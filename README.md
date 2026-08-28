# Plataforma SaaS Multi-tenant de Gestión de PQRS con IA

API multi-tenant en ASP.NET Core que permite a varias empresas centralizar sus
PQRS. Incluye un widget incrustable de una sola línea que primero intenta
resolver la consulta con RAG sobre la base de conocimiento de la empresa y,
si no lo logra, abre un formulario de radicación cuyo ticket se clasifica
automáticamente por IA.

---

## 1. Puesta en marcha

Requisitos: Docker y Docker Compose. Nada más.

```bash
cp .env.example .env          # y llena JWT_SECRET y POSTGRES_PASSWORD
docker compose up -d --build
curl http://localhost:8081/health
```

Eso levanta PostgreSQL con pgvector y la API. Las migraciones se aplican y los
datos semilla se siembran solos al arrancar: no hay pasos manuales.

| Recurso | URL |
|---|---|
| API | http://localhost:8081 |
| Swagger | http://localhost:8081/swagger |
| Salud | http://localhost:8081/health |
| Widget servido por la API | http://localhost:8081/pqrs-widget.js |

### Credenciales de prueba

| Empresa | Slug | Usuario | Contraseña |
|---|---|---|---|
| Empresa Demo | `demo` | `admin@demo.local` | `Admin123!` |
| MóvilNet | `movilnet` | `admin@movilnet.local` | `Admin123!` |

Existen dos empresas a propósito: sin una segunda, el aislamiento multi-tenant
no se puede demostrar.

### Probar el widget

```bash
cd widget && python3 -m http.server 5500
```

Abre http://localhost:5500/demo.html. Ese origen está registrado como dominio
autorizado del tenant `demo`, así que el CORS dinámico lo deja pasar.

`demo-mock.html` es la misma página con la API simulada en el navegador, útil
para revisar la interfaz sin levantar el backend.

---

## 2. Arquitectura

```
┌──────────────────────────────────────────────────────────────┐
│  Sitio de la empresa cliente                                 │
│  <script src="…/pqrs-widget.js" data-tenant="demo"></script> │
└───────────────────────────┬──────────────────────────────────┘
                            │ fetch + X-Tenant-Id
┌───────────────────────────▼──────────────────────────────────┐
│  PqrsPlatform.Api                                            │
│  Controllers · DynamicCorsPolicyProvider                     │
│  TenantResolutionMiddleware · TicketsHub (SignalR)           │
└───────────────────────────┬──────────────────────────────────┘
┌───────────────────────────▼──────────────────────────────────┐
│  PqrsPlatform.Application       (sin dependencias externas)  │
│  DTOs · IEmbeddingService · ILlmService · IJwtTokenService   │
└───────────────────────────┬──────────────────────────────────┘
┌───────────────────────────▼──────────────────────────────────┐
│  PqrsPlatform.Infrastructure                                 │
│  AppDbContext · RagService · TicketTriageService             │
│  AI/ OpenAi · Gemini · Local        Auth/ JwtTokenService    │
└───────────────────────────┬──────────────────────────────────┘
┌───────────────────────────▼──────────────────────────────────┐
│  PqrsPlatform.Domain            (no referencia a nadie)      │
│  Tenant · User · KnowledgeBaseArticle · Ticket               │
│  RagInteraction · Enums · ITenantContext                     │
└──────────────────────────────────────────────────────────────┘
```

Las referencias apuntan siempre hacia adentro. `Domain` no depende de nada, y
cuando `Application` necesita algo de infraestructura no la referencia: declara
una interfaz que `Infrastructure` implementa. `IEmbeddingService` es el caso
claro — la capa de aplicación sabe que existe "algo que genera embeddings",
sin saber si detrás hay OpenAI, Gemini o una implementación local.

---

## 3. Aislamiento multi-tenant

Es el requisito central, y se defiende en tres capas independientes.

**Resolución del tenant.** `TenantResolutionMiddleware` lo determina por dos
vías según quién llama:

| Origen | Fuente del tenant |
|---|---|
| Widget público | header `X-Tenant-Id` con el slug, validado contra la tabla |
| Agente con JWT | claim `tenant_id`, firmado |

El claim tiene prioridad sobre el header. Si fuera al revés, un agente
autenticado podría enviar el header de otra empresa a mano y leer sus tickets.

**Filtros globales de consulta.** Cada entidad con `TenantId` lleva un
`HasQueryFilter` en el `AppDbContext`. Todo `SELECT` queda filtrado aunque el
desarrollador olvide el `Where`. Para saltárselo hay que escribir
`IgnoreQueryFilters()` explícitamente, y solo se usa en el login y el arranque,
donde todavía no hay tenant resuelto.

**Estampado en escritura.** `SaveChangesAsync` recorre las entidades nuevas y
les asigna el `TenantId` del contexto. Los filtros protegen la lectura; esto
protege la escritura.

### Cómo verificarlo

```bash
TOKEN_A=$(curl -s -X POST http://localhost:8081/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.local","password":"Admin123!"}' | jq -r .token)

TOKEN_B=$(curl -s -X POST http://localhost:8081/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@movilnet.local","password":"Admin123!"}' | jq -r .token)

curl -s http://localhost:8081/api/v1/tickets -H "Authorization: Bearer $TOKEN_A"
curl -s http://localhost:8081/api/v1/tickets -H "Authorization: Bearer $TOKEN_B"
```

Cada token devuelve un solo ticket, y ambos se llaman `PQRS-2026-0001`. El
número es único **por tenant**, no globalmente: es la evidencia visible de que
las tablas están particionadas lógicamente.

---

## 4. Base de datos

PostgreSQL 16 con la extensión `pgvector`.

| Entidad | Rol |
|---|---|
| `Tenants` | empresas suscriptoras, slug público y dominios autorizados |
| `Users` | agentes y administradores, uno o más por tenant |
| `KnowledgeBaseArticles` | artículos con columna `vector(1536)` para RAG |
| `Tickets` | PQRS con tipo, prioridad, sentimiento, resumen y estado |
| `RagInteraction` | cada consulta al chat; de aquí sale la métrica de desviación |

### Índices y por qué están

```sql
CREATE INDEX ON "Tickets" ("TenantId", "Status");
CREATE INDEX ON "Tickets" ("TenantId", "Priority");
CREATE INDEX ON "KnowledgeBaseArticles" USING hnsw ("Embedding" vector_cosine_ops);
```

Los B-Tree son compuestos y **empiezan por `TenantId`**. Esto no es
decorativo: como cada consulta lleva el filtro de tenant, un índice que
empiece por `Status` obligaría a escanear filas de todas las empresas antes de
descartarlas. Con `TenantId` primero, el motor salta directo al bloque de la
empresa y filtra dentro. Al crecer el número de tenants, la diferencia se
amplía de forma lineal.

Se eligió **HNSW sobre IVFFlat** para el índice vectorial porque HNSW no
requiere entrenamiento previo con un conjunto representativo. IVFFlat necesita
suficientes filas antes de construir listas útiles, y aquí cada tenant empieza
con una base de conocimiento vacía que crece de a un artículo.

### Decisiones de modelado

**Los campos de IA en `Tickets` son nullables.** Si el proveedor de IA falla o
devuelve un JSON inválido, el ticket se guarda igual sin clasificar y el agente
lo revisa a mano. Un ticket sin clasificar es un inconveniente; una radicación
perdida por un servicio externo caído es una falla del sistema.

**Los enums se persisten como texto.** Cuesta unos bytes más que un `int`, pero
la tabla es legible directamente en `psql` y agregar un valor nuevo no corre la
numeración de los existentes.

**`RagInteraction.ResolvedByUser` es `bool?`.** `null` significa que el usuario
cerró el widget sin contestar, que es un caso distinto de "la respuesta no me
sirvió". Colapsarlos en un `bool` falsearía la métrica de desviación.

---

## 5. Módulo de IA

### Flujo RAG (pre-radicación)

1. El usuario escribe en el widget. La API genera el embedding de la consulta.
2. Se buscan los `RAG_TOP_K` artículos más cercanos por distancia coseno,
   filtrados por `TenantId`.
3. Si el mejor puntaje **no** supera `RAG_SIMILARITY_THRESHOLD`, se devuelve
   `answered: false` sin llamar al LLM. Ahorra una llamada y evita respuestas
   inventadas sobre contexto irrelevante.
4. Si lo supera, el LLM sintetiza una respuesta usando solo esos artículos.
5. El widget pregunta si resolvió la duda.
   - **Sí** → se marca `ResolvedByUser = true`. Ticket desviado, sin registro
     en `Tickets`.
   - **No** → se abre el formulario y el ticket queda enlazado a la interacción.

Toda consulta queda registrada en `RagInteraction`, respondida o no. Las que no
alcanzaron el umbral son la lista de trabajo para mejorar la base de
conocimiento: son preguntas reales que la empresa no sabe responder.

### Triaje (post-radicación)

Al radicar, la IA recibe asunto y descripción y devuelve JSON estricto con
tipo, prioridad, sentimiento y resumen. La respuesta pasa por `AiJson`, que
retira posibles bloques markdown, extrae el objeto y **valida cada valor contra
la lista permitida**. Si el modelo inventa una prioridad "Urgente", cae al valor
por defecto en vez de romper la inserción.

### Diseño del prompt

Los prompts viven en `Infrastructure/Prompts/PromptTemplates.cs`, separados del
código que los usa.

El de RAG impone tres restricciones: responder solo con el contexto entregado,
devolver un marcador explícito si la información no está, y no citar los
artículos. La segunda es la que importa — sin una salida explícita para "no
sé", los modelos rellenan el vacío con texto plausible, que es justo lo
inaceptable en atención al cliente.

El de triaje define cada categoría en lugar de solo nombrarla, porque la
frontera entre *queja* y *reclamo* no es obvia ni siquiera para una persona.
También fija el criterio de prioridad alta (riesgo, afectación económica grave,
caso reincidente) y ordena elegir el valor conservador ante ambigüedad: es
preferible que un agente suba la prioridad de un ticket a que el sistema baje
la de uno urgente.

### Umbral de similitud

`RAG_SIMILARITY_THRESHOLD` se calibra por proveedor, porque los puntajes de
coseno no son comparables entre espacios vectoriales distintos:

| Proveedor | Umbral | Motivo |
|---|---|---|
| OpenAi | 0.75 | valor de referencia para `text-embedding-3-small` |
| Gemini | 0.70 | `text-embedding-004` produce puntajes algo más bajos |
| Local | 0.35 | coincidencia léxica, no semántica |

Bajarlo aumenta la desviación de tickets pero también las respuestas
equivocadas. Subirlo hace el asistente inútil. Se ajusta observando las
interacciones registradas con puntaje cercano al límite.

### Tres proveedores intercambiables

`AiServiceCollectionExtensions` elige la implementación al arrancar:

1. `AI_PROVIDER` explícito (`OpenAi`, `Gemini`, `Local`), o
2. hay `OPENAI_API_KEY` → OpenAi, o
3. hay `GEMINI_API_KEY` → Gemini, o
4. ninguna → Local

El modo **Local** no llama a ningún servicio externo: genera embeddings por
bolsa de palabras con hashing y clasifica por reglas. No compite con un LLM,
pero permite que el sistema completo funcione sin conexión ni credenciales.

Gemini devuelve 768 dimensiones y la columna es `vector(1536)`.
`EmbeddingMath.Fit` completa con ceros. Es seguro: la similitud coseno entre
dos vectores rellenados con ceros en las mismas posiciones es idéntica a la de
los originales, porque esas dimensiones no aportan al producto punto ni a las
magnitudes.

> **No mezclar proveedores en una misma base.** Los vectores de proveedores
> distintos viven en espacios distintos y compararlos da resultados sin
> sentido. Al cambiar de proveedor hay que regenerar los embeddings:
> ```bash
> docker compose exec db psql -U pqrs -d pqrsdb \
>   -c 'UPDATE "KnowledgeBaseArticles" SET "Embedding" = NULL;'
> curl -X POST http://localhost:8081/api/v1/kb-articles/reindex \
>   -H "Authorization: Bearer $TOKEN"
> ```

---

## 6. Endpoints

### Públicos (widget) — requieren header `X-Tenant-Id`

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/v1/widget/rag-search` | consulta la base de conocimiento |
| POST | `/api/v1/widget/rag-feedback` | registra si la respuesta resolvió |
| POST | `/api/v1/widget/tickets` | radica la PQRS con triaje automático |

### Protegidos (agentes) — requieren `Authorization: Bearer`

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/v1/auth/login` | emite el JWT |
| GET | `/api/v1/auth/me` | verifica el token y el tenant resuelto |
| GET/POST/PUT/DELETE | `/api/v1/kb-articles` | CRUD con embedding automático |
| POST | `/api/v1/kb-articles/reindex` | regenera embeddings pendientes |
| GET | `/api/v1/tickets` | lista con filtros de estado y prioridad |
| GET | `/api/v1/tickets/{id}` | detalle |
| PATCH | `/api/v1/tickets/{id}` | actualiza estado o asignación |

### Tiempo real

`TicketsHub` en `/hubs/tickets`, con grupos por tenant. Al radicarse un ticket
de prioridad **Alta** o sentimiento **Negativo**, se emite `CriticalTicket`
únicamente al grupo de esa empresa. La conexión exige JWT y el grupo se deduce
del claim, no de un parámetro del cliente.

---

## 7. Incrustar el widget

Una sola línea en el sitio de la empresa:

```html
<script src="https://tu-api.com/pqrs-widget.js"
        data-tenant="demo"
        data-api="https://tu-api.com"></script>
```

| Atributo | Obligatorio | Por defecto |
|---|---|---|
| `data-tenant` | sí | — |
| `data-api` | no | `http://localhost:8081` |
| `data-title` | no | `PQRS Assistant` |
| `data-brand` | no | `#321E48` |
| `data-accent` | no | `#65DCD5` |
| `data-position` | no | `right` |

El widget se monta en un **Shadow DOM**, así que sus estilos no afectan al
sitio anfitrión ni el CSS del sitio lo deforma. No tiene dependencias.

El dominio debe estar en `AllowedOrigins` del tenant, o el navegador bloqueará
las peticiones en el preflight. Ese rechazo es el CORS dinámico funcionando.

---

## 8. Variables de entorno

| Variable | Descripción |
|---|---|
| `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` | credenciales de la base |
| `POSTGRES_HOST_PORT` | puerto expuesto en el anfitrión (5433 por defecto) |
| `CONNECTION_STRING` | solo para correr la API fuera de Docker |
| `JWT_SECRET` | mínimo 32 caracteres — `openssl rand -base64 48` |
| `JWT_ISSUER` / `JWT_AUDIENCE` / `JWT_EXPIRATION_MINUTES` | parámetros del token |
| `AI_PROVIDER` | `OpenAi`, `Gemini`, `Local` o vacío para automático |
| `OPENAI_API_KEY` / `EMBEDDING_MODEL` / `LLM_MODEL` | configuración de OpenAI |
| `GEMINI_API_KEY` / `GEMINI_EMBEDDING_MODEL` / `GEMINI_LLM_MODEL` | configuración de Gemini |
| `RAG_SIMILARITY_THRESHOLD` | umbral de coseno |
| `RAG_TOP_K` | artículos enviados como contexto |
| `API_HOST_PORT` | puerto de la API en el anfitrión (8081 por defecto) |

Dentro de `docker-compose` la cadena de conexión usa `Host=db;Port=5432`: el
host es el nombre del servicio, porque cada contenedor tiene su propia red y
`localhost` apuntaría al contenedor mismo.

---

## 9. Estructura

```
pqrs-ai-platform/
├── docker-compose.yml
├── .env.example
├── backend/
│   ├── Dockerfile
│   └── src/
│       ├── PqrsPlatform.Domain/          entidades, enums, interfaces
│       ├── PqrsPlatform.Application/     DTOs y contratos de servicio
│       ├── PqrsPlatform.Infrastructure/  EF Core, IA, auth, persistencia
│       └── PqrsPlatform.Api/             controllers, middleware, hubs
├── widget/
│   ├── pqrs-widget.js
│   ├── demo.html                         contra la API real
│   └── demo-mock.html                    con API simulada
└── docs/
```

---

## 10. Problemas frecuentes

**`port is already allocated` al levantar.** Otro Postgres tiene tomado el
puerto. Cambia `POSTGRES_HOST_PORT` en el `.env`, o detén el contenedor previo
con `docker stop`.

**El RAG nunca responde.** Los artículos no tienen embedding. Revisa el log con
`docker compose logs api | grep -i embedding` y llama a
`POST /api/v1/kb-articles/reindex`. Si usas el modo Local, verifica que el
umbral esté en 0.35 y no en 0.75.

**El widget no conecta desde otro dominio.** El origen no está en
`AllowedOrigins` de ese tenant. Es el comportamiento esperado.

**Cambios en `AllowedOrigins` que no se reflejan.** Los orígenes se cachean 60
segundos para no consultar la base en cada preflight. Espera o reinicia la API.
