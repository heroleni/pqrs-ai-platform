/*!
 * pqrs-widget.js — Widget incrustable de PQRS con auto-atención por IA.
 * Vanilla JS, sin dependencias. Aislado con Shadow DOM.
 * Sistema visual: Ethereal SaaS (morado profundo + teal, Inter).
 *
 * Uso:
 *   <script src="https://cdn.tu-saas.com/pqrs-widget.js"
 *           data-tenant="ID_EMPRESA"
 *           data-api="https://api.tu-saas.com"
 *           data-title="PQRS Assistant"
 *           data-brand="#321E48"
 *           data-accent="#65DCD5"></script>
 */
(function () {
  'use strict';

  /* ------------------------------------------------------------------ *
   * 1. Configuración leída del <script>
   * ------------------------------------------------------------------ */
  var host =
      document.currentScript ||
      (function () {
        var all = document.getElementsByTagName('script');
        return all[all.length - 1];
      })();

  var CFG = {
    tenant: host.getAttribute('data-tenant') || '',
    api: (host.getAttribute('data-api') || 'http://localhost:8081').replace(/\/+$/, ''),
    title: host.getAttribute('data-title') || 'PQRS Assistant',
    subtitle: host.getAttribute('data-subtitle') || 'En línea',
    brand: host.getAttribute('data-brand') || '#321E48',   // morado: encabezado y burbuja del usuario
    accent: host.getAttribute('data-accent') || '#65DCD5', // teal: acciones y estados activos
    position: host.getAttribute('data-position') || 'right'
  };

  if (!CFG.tenant) {
    console.error('[pqrs-widget] Falta el atributo data-tenant en la etiqueta <script>.');
    return;
  }
  if (window.__pqrsWidgetLoaded) return;
  window.__pqrsWidgetLoaded = true;

  /* ------------------------------------------------------------------ *
   * 2. Estado
   * ------------------------------------------------------------------ */
  var S = {
    open: false,
    phase: 'chat', // chat | form | done
    busy: false,
    interactionId: null,
    lastQuery: '',
    ticket: null
  };

  var ENDPOINTS = {
    ragSearch: '/api/v1/widget/rag-search',
    ragFeedback: '/api/v1/widget/rag-feedback',
    tickets: '/api/v1/widget/tickets'
  };

  /* ------------------------------------------------------------------ *
   * 3. Estilos — tokens del sistema Ethereal SaaS
   * ------------------------------------------------------------------ */
  var CSS = [
    "@import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');",

    ':host { all: initial; }',
    '*, *::before, *::after { box-sizing: border-box; }',

    '.root {',
    '  --brand: ' + CFG.brand + ';',        /* #321E48 morado profundo */
    '  --accent: ' + CFG.accent + ';',      /* #65DCD5 teal, color de acción */
    '  --slate: #43637E;',                  /* azul pizarra: bordes y texto de apoyo */
    '  --mint: #D9FFF4;',                   /* fondo de burbujas del asistente */
    '  --mint-deep: #CAF0E5;',
    '  --ink: #00201B;',
    '  --ink-soft: #4A454D;',
    '  --line: rgba(67, 99, 126, .15);',    /* slate al 15%, en vez de sombras pesadas */
    '  --error: #BA1A1A;',
    '  --error-bg: #FFDAD6;',
    '  --r-sm: 8px; --r-md: 12px; --r-lg: 16px;',
    '  --font: "Inter", -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;',
    '  position: fixed; bottom: 24px; z-index: 2147483000;',
    '  font-family: var(--font); color: var(--ink); line-height: 1.5;',
    '  -webkit-font-smoothing: antialiased;',
    '}',
    '.root[data-position="right"] { right: 24px; }',
    '.root[data-position="left"]  { left: 24px; }',

    /* --- Botón flotante: teal con icono morado --- */
    '.launcher {',
    '  display: grid; place-items: center; width: 56px; height: 56px;',
    '  border: 0; border-radius: 9999px; cursor: pointer;',
    '  background: var(--accent); color: var(--brand);',
    '  box-shadow: 0 4px 20px rgba(50,30,72,.18);',
    '  transition: transform .2s ease, box-shadow .2s ease;',
    '}',
    '.launcher:hover { transform: translateY(-2px); box-shadow: 0 8px 28px rgba(50,30,72,.26); }',
    '.launcher:focus-visible { outline: 3px solid var(--brand); outline-offset: 3px; }',
    '.launcher svg { width: 24px; height: 24px; }',
    '.root[data-open="true"] .launcher { display: none; }',

    /* --- Panel flotante con efecto de vidrio --- */
    '.panel {',
    '  display: none; flex-direction: column;',
    '  width: 384px; height: 588px; max-height: calc(100vh - 48px);',
    '  border-radius: var(--r-lg); overflow: hidden;',
    '  background: rgba(255,255,255,.82);',
    '  -webkit-backdrop-filter: blur(12px); backdrop-filter: blur(12px);',
    '  border: 1px solid var(--line);',
    '  box-shadow: 0 4px 20px rgba(50,30,72,.08);',
    '}',
    '.root[data-open="true"] .panel { display: flex; animation: rise .24s cubic-bezier(.2,.8,.3,1); }',
    '@keyframes rise { from { opacity: 0; transform: translateY(14px); } to { opacity: 1; transform: none; } }',

    /* --- Encabezado morado --- */
    '.head { display: flex; align-items: center; gap: 12px; padding: 18px 16px 18px 18px; background: var(--brand); color: #fff; }',
    '.avatar { width: 34px; height: 34px; flex: none; display: grid; place-items: center; border-radius: 9999px; background: rgba(255,255,255,.14); }',
    '.avatar svg { width: 18px; height: 18px; }',
    '.head .grow { flex: 1; min-width: 0; }',
    '.head h1 { margin: 0; font-size: 16px; font-weight: 600; letter-spacing: -.01em; }',
    '.head .sub { display: flex; align-items: center; gap: 6px; margin: 3px 0 0; font-size: 11px; font-weight: 600; letter-spacing: .05em; text-transform: uppercase; color: var(--accent); }',
    '.head .sub::before { content: ""; width: 6px; height: 6px; border-radius: 50%; background: var(--accent); }',
    '.iconbtn { width: 32px; height: 32px; display: grid; place-items: center; border: 0; border-radius: var(--r-sm); background: transparent; color: #fff; cursor: pointer; }',
    '.iconbtn:hover { background: rgba(255,255,255,.16); }',
    '.iconbtn:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }',
    '.iconbtn svg { width: 16px; height: 16px; }',

    /* Pasos como puntos teal, según el sistema */
    '.steps { display: flex; align-items: center; justify-content: center; gap: 8px; padding: 12px; background: var(--brand); }',
    '.steps i { width: 7px; height: 7px; border-radius: 50%; background: rgba(101,220,213,.28); transition: background .2s, transform .2s; }',
    '.steps i[data-on="1"] { background: var(--accent); transform: scale(1.15); }',

    /* --- Conversación --- */
    '.stream { flex: 1; overflow-y: auto; padding: 20px 16px 10px; background: transparent; scroll-behavior: smooth; }',
    '.msg { display: flex; gap: 8px; margin-bottom: 14px; }',
    '.msg .bubble { max-width: 78%; padding: 11px 14px; border-radius: var(--r-md); font-size: 14px; white-space: pre-wrap; word-wrap: break-word; }',
    '.msg[data-from="bot"] .bubble { background: var(--mint); color: var(--ink); border-top-left-radius: 4px; }',
    '.msg[data-from="bot"]::before { content: ""; width: 26px; height: 26px; flex: none; border-radius: 9999px; background: var(--brand) url("data:image/svg+xml,%3Csvg xmlns=\'http://www.w3.org/2000/svg\' viewBox=\'0 0 24 24\' fill=\'none\' stroke=\'%2365DCD5\' stroke-width=\'2\' stroke-linecap=\'round\' stroke-linejoin=\'round\'%3E%3Crect x=\'4\' y=\'8\' width=\'16\' height=\'12\' rx=\'3\'/%3E%3Cpath d=\'M12 4v4M9 14h.01M15 14h.01\'/%3E%3C/svg%3E") center/15px no-repeat; }',
    '.msg[data-from="me"] { justify-content: flex-end; }',
    '.msg[data-from="me"] .bubble { background: var(--brand); color: #fff; border-top-right-radius: 4px; }',
    '.msg[data-tone="warn"] .bubble { background: var(--error-bg); color: #93000A; }',

    '.src { margin: 8px 0 0; font-size: 11px; font-weight: 600; letter-spacing: .05em; color: var(--slate); }',

    /* Confirmación sí/no: primaria teal, secundaria fantasma */
    '.confirm { display: flex; flex-direction: column; gap: 8px; margin: 0 0 14px 34px; }',
    '.confirm button { padding: 10px 14px; border-radius: var(--r-sm); cursor: pointer; font: 600 13px var(--font); text-align: left; }',
    '.confirm button[data-a="si"] { background: var(--accent); color: var(--brand); border: 0; }',
    '.confirm button[data-a="no"] { background: transparent; color: var(--slate); border: 1px solid var(--line); }',
    '.confirm button:hover { filter: brightness(.96); }',
    '.confirm button[data-a="no"]:hover { border-color: var(--slate); color: var(--brand); }',
    '.confirm button:focus-visible { outline: 2px solid var(--brand); outline-offset: 2px; }',

    /* Escribiendo… */
    '.dots { display: inline-flex; gap: 4px; padding: 4px 0; }',
    '.dots i { width: 6px; height: 6px; border-radius: 50%; background: var(--slate); opacity: .4; animation: blink 1.1s infinite; }',
    '.dots i:nth-child(2) { animation-delay: .18s; } .dots i:nth-child(3) { animation-delay: .36s; }',
    '@keyframes blink { 0%,60%,100% { opacity: .25; } 30% { opacity: .9; } }',

    /* --- Barra de escritura --- */
    '.composer { display: flex; gap: 8px; padding: 14px; border-top: 1px solid var(--line); background: rgba(255,255,255,.6); }',
    '.composer textarea {',
    '  flex: 1; resize: none; height: 44px; max-height: 112px; padding: 12px 14px;',
    '  border: 1px solid var(--line); border-radius: var(--r-md);',
    '  font: 400 14px/1.35 var(--font); color: var(--ink); background: #fff;',
    '}',
    '.composer textarea::placeholder { color: var(--slate); opacity: .7; }',
    '.composer textarea:focus { outline: none; border-color: var(--accent); box-shadow: 0 0 0 3px rgba(101,220,213,.25); }',
    '.send { width: 44px; height: 44px; flex: none; display: grid; place-items: center; border: 0; border-radius: var(--r-md); background: var(--accent); color: var(--brand); cursor: pointer; }',
    '.send:disabled { opacity: .45; cursor: not-allowed; }',
    '.send:focus-visible { outline: 2px solid var(--brand); outline-offset: 2px; }',
    '.send svg { width: 18px; height: 18px; }',

    /* --- Formulario --- */
    '.form { flex: 1; overflow-y: auto; padding: 20px 18px 22px; background: rgba(255,255,255,.55); }',
    '.form .lead { margin: 0 0 18px; font-size: 14px; color: var(--ink-soft); }',
    '.field { margin-bottom: 14px; }',
    '.field label { display: block; margin-bottom: 6px; font-size: 12px; font-weight: 600; letter-spacing: .05em; text-transform: uppercase; color: var(--slate); }',
    '.field input, .field textarea {',
    '  width: 100%; padding: 11px 14px; border: 1px solid var(--slate); border-radius: var(--r-md);',
    '  font: 400 14px/1.4 var(--font); color: var(--ink); background: #fff;',
    '}',
    '.field textarea { min-height: 100px; resize: vertical; }',
    '.field input:focus, .field textarea:focus { outline: none; border-color: var(--accent); box-shadow: 0 0 0 3px rgba(101,220,213,.25); }',
    '.field[data-invalid="1"] input, .field[data-invalid="1"] textarea { border-color: var(--error); }',
    '.hint { margin: 6px 0 0; font-size: 12px; color: var(--error); }',
    '.submit { width: 100%; margin-top: 8px; padding: 14px; border: 0; border-radius: var(--r-sm); background: var(--accent); color: var(--brand); cursor: pointer; font: 600 14px var(--font); }',
    '.submit:disabled { opacity: .55; cursor: progress; }',
    '.submit:focus-visible { outline: 3px solid var(--brand); outline-offset: 2px; }',
    '.back { margin-top: 10px; width: 100%; padding: 10px; background: none; border: 0; cursor: pointer; font: 500 13px var(--font); color: var(--slate); }',
    '.back:hover { color: var(--brand); }',
    '.formerror { margin: 0 0 16px; padding: 11px 14px; border-radius: var(--r-sm); background: var(--error-bg); color: #93000A; font-size: 13px; }',

    /* --- Comprobante de radicación --- */
    '.done { flex: 1; display: flex; flex-direction: column; justify-content: center; padding: 24px; }',
    '.stub { background: #fff; border: 1px solid var(--line); border-radius: var(--r-md); overflow: hidden; box-shadow: 0 4px 20px rgba(50,30,72,.08); }',
    '.stub header { padding: 18px 20px 16px; background: var(--brand); color: #fff; }',
    '.stub header b { display: block; font-size: 16px; font-weight: 600; }',
    '.stub header span { display: block; margin-top: 4px; font-size: 13px; opacity: .78; }',
    '.stub .num { padding: 18px 20px; text-align: center; border-bottom: 1px solid var(--line); background: var(--mint); }',
    '.stub .num small { display: block; font-size: 12px; font-weight: 600; letter-spacing: .05em; text-transform: uppercase; color: var(--slate); }',
    '.stub .num strong { display: block; margin-top: 8px; font-size: 24px; font-weight: 700; letter-spacing: .04em; color: var(--brand); }',
    '.stub dl { display: grid; grid-template-columns: auto 1fr; gap: 10px 16px; margin: 0; padding: 16px 20px 18px; font-size: 14px; }',
    '.stub dt { font-size: 12px; font-weight: 600; letter-spacing: .05em; text-transform: uppercase; color: var(--slate); align-self: center; }',
    '.stub dd { margin: 0; text-align: right; font-weight: 600; }',
    '.done .again { margin-top: 18px; width: 100%; padding: 12px; border: 1px solid var(--line); border-radius: var(--r-sm); background: transparent; cursor: pointer; font: 600 13px var(--font); color: var(--slate); }',
    '.done .again:hover { border-color: var(--slate); color: var(--brand); }',

    '[hidden] { display: none !important; }',
    '.sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }',

    /* --- Responsive --- */
    '@media (max-width: 480px) {',
    '  .root { bottom: 16px; left: 16px; right: 16px; }',
    '  .panel { width: auto; height: calc(100vh - 32px); }',
    '}',
    '@media (prefers-reduced-motion: reduce) {',
    '  .root *, .root *::before { animation: none !important; transition: none !important; }',
    '}'
  ].join('\n');

  /* ------------------------------------------------------------------ *
   * 4. Marcado
   * ------------------------------------------------------------------ */
  var ICON = {
    chat: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 11.5a8.4 8.4 0 0 1-9 8.4 8.5 8.5 0 0 1-3.8-.9L3 21l1.9-5.1A8.4 8.4 0 0 1 12 3a8.4 8.4 0 0 1 9 8.5z"/></svg>',
    bot: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="4" y="8" width="16" height="12" rx="3"/><path d="M12 4v4M9 14h.01M15 14h.01"/></svg>',
    close: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M18 6 6 18M6 6l12 12"/></svg>',
    send: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m22 2-7 20-4-9-9-4 20-7z"/></svg>'
  };

  function esc(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  var HTML =
      '<div class="root" data-position="' + CFG.position + '" data-open="false">' +
      '<button class="launcher" type="button" aria-label="Abrir ' + esc(CFG.title) + '" aria-haspopup="dialog">' +
      ICON.chat +
      '</button>' +

      '<section class="panel" role="dialog" aria-modal="false" aria-label="' + esc(CFG.title) + '">' +
      '<div class="head">' +
      '<span class="avatar" aria-hidden="true">' + ICON.bot + '</span>' +
      '<div class="grow"><h1>' + esc(CFG.title) + '</h1><p class="sub">' + esc(CFG.subtitle) + '</p></div>' +
      '<button class="iconbtn js-close" type="button" aria-label="Cerrar">' + ICON.close + '</button>' +
      '</div>' +
      '<div class="steps" aria-hidden="true"><i data-on="1"></i><i></i></div>' +

      /* Fase 1 — chat RAG */
      '<div class="stream js-stream" role="log" aria-live="polite"></div>' +
      '<div class="composer js-composer">' +
      '<label class="sr" for="pqrs-q">Escribe tu consulta</label>' +
      '<textarea id="pqrs-q" class="js-input" rows="1" placeholder="Escribe tu mensaje…"></textarea>' +
      '<button class="send js-send" type="button" aria-label="Enviar consulta">' + ICON.send + '</button>' +
      '</div>' +

      /* Fase 2 — formulario de radicación */
      '<div class="form js-form" hidden>' +
      '<p class="lead">Radica tu solicitud y te damos un número de seguimiento. Un agente la revisará.</p>' +
      '<div class="formerror js-formerror" hidden></div>' +
      '<div class="field" data-f="name"><label for="pqrs-name">Nombre</label><input id="pqrs-name" autocomplete="name"><p class="hint"></p></div>' +
      '<div class="field" data-f="email"><label for="pqrs-email">Correo</label><input id="pqrs-email" type="email" autocomplete="email"><p class="hint"></p></div>' +
      '<div class="field" data-f="subject"><label for="pqrs-subject">Asunto</label><input id="pqrs-subject"><p class="hint"></p></div>' +
      '<div class="field" data-f="description"><label for="pqrs-desc">Descripción</label><textarea id="pqrs-desc"></textarea><p class="hint"></p></div>' +
      '<button class="submit js-submit" type="button">Radicar solicitud</button>' +
      '<button class="back js-back" type="button">Volver al chat</button>' +
      '</div>' +

      /* Fase 3 — comprobante */
      '<div class="done js-done" hidden>' +
      '<div class="stub">' +
      '<header><b>Solicitud radicada</b><span>Te escribiremos al correo registrado.</span></header>' +
      '<div class="num"><small>Número de radicado</small><strong class="js-radicado">—</strong></div>' +
      '<dl>' +
      '<dt>Tipo</dt><dd class="js-tipo">—</dd>' +
      '<dt>Prioridad</dt><dd class="js-prioridad">—</dd>' +
      '<dt>Estado</dt><dd>Pendiente</dd>' +
      '</dl>' +
      '</div>' +
      '<button class="again js-again" type="button">Hacer otra consulta</button>' +
      '</div>' +
      '</section>' +
      '</div>';

  /* ------------------------------------------------------------------ *
   * 5. Montaje
   * ------------------------------------------------------------------ */
  var mount = document.createElement('div');
  mount.setAttribute('data-pqrs-widget', CFG.tenant);
  var shadow = mount.attachShadow ? mount.attachShadow({ mode: 'open' }) : mount;
  var style = document.createElement('style');
  style.textContent = CSS;
  shadow.appendChild(style);
  var wrap = document.createElement('div');
  wrap.innerHTML = HTML;
  shadow.appendChild(wrap.firstChild);

  function ready(fn) {
    if (document.body) fn();
    else document.addEventListener('DOMContentLoaded', fn);
  }
  ready(function () { document.body.appendChild(mount); });

  var $ = function (sel) { return shadow.querySelector(sel); };
  var root = $('.root');
  var stream = $('.js-stream');
  var input = $('.js-input');
  var sendBtn = $('.js-send');
  var composer = $('.js-composer');
  var formEl = $('.js-form');
  var doneEl = $('.js-done');
  var steps = shadow.querySelectorAll('.steps i');

  /* ------------------------------------------------------------------ *
   * 6. Utilidades
   * ------------------------------------------------------------------ */
  function scrollEnd() { stream.scrollTop = stream.scrollHeight; }

  function say(from, text, tone) {
    var el = document.createElement('div');
    el.className = 'msg';
    el.setAttribute('data-from', from);
    if (tone) el.setAttribute('data-tone', tone);
    el.innerHTML = '<div class="bubble">' + esc(text) + '</div>';
    stream.appendChild(el);
    scrollEnd();
    return el;
  }

  function typing() {
    var el = document.createElement('div');
    el.className = 'msg';
    el.setAttribute('data-from', 'bot');
    el.innerHTML = '<div class="bubble"><span class="dots"><i></i><i></i><i></i></span></div>';
    stream.appendChild(el);
    scrollEnd();
    return el;
  }

  function askConfirm(onYes, onNo) {
    var box = document.createElement('div');
    box.className = 'confirm';
    box.innerHTML =
        '<button type="button" data-a="si">Sí, resolvió mi duda</button>' +
        '<button type="button" data-a="no">No, quiero radicar una PQRS</button>';
    box.addEventListener('click', function (e) {
      var b = e.target.closest('button');
      if (!b) return;
      box.remove();
      say('me', b.getAttribute('data-a') === 'si' ? 'Sí, resolvió mi duda' : 'No, quiero radicar una PQRS');
      (b.getAttribute('data-a') === 'si' ? onYes : onNo)();
    });
    stream.appendChild(box);
    scrollEnd();
  }

  function api(path, body) {
    return fetch(CFG.api + path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': CFG.tenant },
      body: JSON.stringify(body)
    }).then(function (r) {
      if (!r.ok) throw new Error('HTTP ' + r.status);
      return r.status === 204 ? {} : r.json();
    });
  }

  function setPhase(phase) {
    S.phase = phase;
    var chat = phase === 'chat';
    stream.hidden = !chat;
    composer.hidden = !chat;
    formEl.hidden = phase !== 'form';
    doneEl.hidden = phase !== 'done';
    steps[1].setAttribute('data-on', chat ? '0' : '1');
    $('.sub').textContent =
        phase === 'form' ? 'Paso 2 de 2' :
            phase === 'done' ? 'Registrada' : CFG.subtitle;
  }

  /* ------------------------------------------------------------------ *
   * 7. Fase 1 — consulta RAG
   * ------------------------------------------------------------------ */
  function greet() {
    stream.innerHTML = '';
    say('bot', 'Hola, soy el asistente. Pregúntame lo que necesites: si ya tenemos la respuesta, te la doy aquí mismo. Si no, radicamos tu PQRS.');
  }

  function ask() {
    var q = input.value.trim();
    if (!q || S.busy) return;

    say('me', q);
    input.value = '';
    input.style.height = '44px';
    S.lastQuery = q;
    S.busy = true;
    sendBtn.disabled = true;
    var t = typing();

    api(ENDPOINTS.ragSearch, { query: q })
        .then(function (res) {
          t.remove();
          S.interactionId = res.interactionId || null;

          if (res.answered && res.answer) {
            var m = say('bot', res.answer);
            if (res.sources && res.sources.length) {
              var s = document.createElement('p');
              s.className = 'src';
              s.textContent = 'Basado en: ' + res.sources.slice(0, 3).join(' · ');
              m.querySelector('.bubble').appendChild(s);
            }
            say('bot', '¿Esta respuesta resolvió tu inquietud?');
            askConfirm(deflect, toForm);
          } else {
            say('bot', 'No encontré esa información en nuestra base de conocimiento. Vamos a radicarla para que un agente la revise.');
            setTimeout(toForm, 700);
          }
        })
        .catch(function () {
          t.remove();
          say('bot', 'No pudimos consultar el asistente. Puedes radicar tu solicitud directamente.', 'warn');
          askConfirm(function () { say('bot', 'Perfecto. Aquí estamos si necesitas algo más.'); }, toForm);
        })
        .then(function () {
          S.busy = false;
          sendBtn.disabled = false;
          input.focus();
        });
  }

  function deflect() {
    if (S.interactionId) api(ENDPOINTS.ragFeedback, { interactionId: S.interactionId, resolved: true }).catch(function () {});
    say('bot', 'Excelente. Quedamos atentos si surge algo más.');
  }

  function toForm() {
    if (S.interactionId) api(ENDPOINTS.ragFeedback, { interactionId: S.interactionId, resolved: false }).catch(function () {});
    var d = $('#pqrs-desc');
    if (!d.value && S.lastQuery) d.value = S.lastQuery;
    setPhase('form');
    $('#pqrs-name').focus();
  }

  /* ------------------------------------------------------------------ *
   * 8. Fase 2 — formulario
   * ------------------------------------------------------------------ */
  var FIELDS = {
    name: { el: '#pqrs-name', label: 'Escribe tu nombre.' },
    email: { el: '#pqrs-email', label: 'Escribe un correo válido.' },
    subject: { el: '#pqrs-subject', label: 'Resume tu solicitud en una frase.' },
    description: { el: '#pqrs-desc', label: 'Cuéntanos qué pasó, con el mayor detalle posible.' }
  };

  function validate() {
    var ok = true, first = null;
    Object.keys(FIELDS).forEach(function (k) {
      var box = shadow.querySelector('.field[data-f="' + k + '"]');
      var el = $(FIELDS[k].el);
      var v = el.value.trim();
      var bad = k === 'email' ? !/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(v) : v.length < (k === 'description' ? 10 : 2);
      box.setAttribute('data-invalid', bad ? '1' : '0');
      box.querySelector('.hint').textContent = bad ? FIELDS[k].label : '';
      if (bad && !first) first = el;
      if (bad) ok = false;
    });
    if (first) first.focus();
    return ok;
  }

  function submit() {
    var err = $('.js-formerror');
    err.hidden = true;
    if (!validate()) return;

    var btn = $('.js-submit');
    btn.disabled = true;
    btn.textContent = 'Radicando…';

    api(ENDPOINTS.tickets, {
      customerName: $('#pqrs-name').value.trim(),
      customerEmail: $('#pqrs-email').value.trim(),
      subject: $('#pqrs-subject').value.trim(),
      description: $('#pqrs-desc').value.trim(),
      ragInteractionId: S.interactionId
    })
        .then(function (res) {
          S.ticket = res;
          $('.js-radicado').textContent = res.ticketNumber || res.id || '—';
          $('.js-tipo').textContent = res.type || 'Por clasificar';
          $('.js-prioridad').textContent = res.priority || 'Por definir';
          setPhase('done');
        })
        .catch(function () {
          err.hidden = false;
          err.textContent = 'No se pudo radicar la solicitud. Revisa tu conexión e inténtalo otra vez.';
        })
        .then(function () {
          btn.disabled = false;
          btn.textContent = 'Radicar solicitud';
        });
  }

  function reset() {
    Object.keys(FIELDS).forEach(function (k) {
      $(FIELDS[k].el).value = '';
      shadow.querySelector('.field[data-f="' + k + '"]').setAttribute('data-invalid', '0');
    });
    S.interactionId = null;
    S.lastQuery = '';
    greet();
    setPhase('chat');
  }

  /* ------------------------------------------------------------------ *
   * 9. Eventos
   * ------------------------------------------------------------------ */
  function toggle(open) {
    S.open = open;
    root.setAttribute('data-open', open ? 'true' : 'false');
    if (open) {
      if (!stream.childElementCount) greet();
      setTimeout(function () { (S.phase === 'chat' ? input : $('#pqrs-name')).focus(); }, 60);
    } else {
      $('.launcher').focus();
    }
  }

  $('.launcher').addEventListener('click', function () { toggle(true); });
  $('.js-close').addEventListener('click', function () { toggle(false); });
  sendBtn.addEventListener('click', ask);
  $('.js-submit').addEventListener('click', submit);
  $('.js-back').addEventListener('click', function () { setPhase('chat'); input.focus(); });
  $('.js-again').addEventListener('click', reset);

  input.addEventListener('keydown', function (e) {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); ask(); }
  });
  input.addEventListener('input', function () {
    input.style.height = '44px';
    input.style.height = Math.min(input.scrollHeight, 112) + 'px';
  });
  shadow.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && S.open) toggle(false);
  });

  /* API pública mínima */
  window.PQRSWidget = {
    open: function () { toggle(true); },
    close: function () { toggle(false); },
    reset: reset
  };
})();