/*!
 * pqrs-widget.js — Widget incrustable de PQRS con auto-atención por IA.
 * Vanilla JS, sin dependencias. Aislado con Shadow DOM.
 *
 * Uso:
 *   <script src="https://cdn.tu-saas.com/pqrs-widget.js"
 *           data-tenant="ID_EMPRESA"
 *           data-api="https://api.tu-saas.com"
 *           data-title="Soporte"
 *           data-accent="#0E5C55"></script>
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
    api: (host.getAttribute('data-api') || 'http://localhost:8080').replace(/\/+$/, ''),
    title: host.getAttribute('data-title') || 'Soporte',
    subtitle: host.getAttribute('data-subtitle') || 'Respondemos al instante',
    accent: host.getAttribute('data-accent') || '#0E5C55',
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
    interactionId: null, // id de la última búsqueda RAG
    lastQuery: '',
    ticket: null
  };

  var ENDPOINTS = {
    ragSearch: '/api/v1/widget/rag-search',
    ragFeedback: '/api/v1/widget/rag-feedback',
    tickets: '/api/v1/widget/tickets'
  };

  /* ------------------------------------------------------------------ *
   * 3. Estilos (viven dentro del Shadow DOM, no tocan la página anfitriona)
   * ------------------------------------------------------------------ */
  var CSS = [
    ':host { all: initial; }',
    '*, *::before, *::after { box-sizing: border-box; }',

    '.root {',
    '  --accent: ' + CFG.accent + ';',
    '  --accent-soft: color-mix(in srgb, var(--accent) 10%, #ffffff);',
    '  --accent-line: color-mix(in srgb, var(--accent) 22%, #ffffff);',
    '  --ink: #14181D;',
    '  --ink-2: #5A646F;',
    '  --line: #E3E7EA;',
    '  --surface: #FFFFFF;',
    '  --canvas: #F7F8F8;',
    '  --danger: #A6301F;',
    '  --radius: 14px;',
    '  --sans: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;',
    '  --mono: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, monospace;',
    '  position: fixed; bottom: 20px; z-index: 2147483000;',
    '  font-family: var(--sans); color: var(--ink); line-height: 1.5;',
    '  -webkit-font-smoothing: antialiased;',
    '}',
    '.root[data-position="right"] { right: 20px; }',
    '.root[data-position="left"]  { left: 20px; }',

    /* --- Botón flotante --- */
    '.launcher {',
    '  display: flex; align-items: center; gap: 9px;',
    '  height: 52px; padding: 0 20px 0 17px; border: 0; border-radius: 26px;',
    '  background: var(--accent); color: #fff; cursor: pointer;',
    '  font: 600 15px/1 var(--sans); letter-spacing: .1px;',
    '  box-shadow: 0 6px 20px rgba(20,24,29,.20);',
    '  transition: transform .18s ease, box-shadow .18s ease;',
    '}',
    '.launcher:hover { transform: translateY(-2px); box-shadow: 0 10px 26px rgba(20,24,29,.26); }',
    '.launcher:focus-visible { outline: 3px solid var(--accent); outline-offset: 3px; }',
    '.launcher svg { width: 20px; height: 20px; }',
    '.root[data-open="true"] .launcher { display: none; }',

    /* --- Panel --- */
    '.panel {',
    '  display: none; flex-direction: column;',
    '  width: 384px; height: 574px; max-height: calc(100vh - 40px);',
    '  background: var(--surface); border: 1px solid var(--line);',
    '  border-radius: var(--radius); overflow: hidden;',
    '  box-shadow: 0 24px 60px rgba(20,24,29,.22);',
    '}',
    '.root[data-open="true"] .panel { display: flex; animation: rise .22s cubic-bezier(.2,.8,.3,1); }',
    '@keyframes rise { from { opacity: 0; transform: translateY(12px); } to { opacity: 1; transform: none; } }',

    /* --- Encabezado --- */
    '.head {',
    '  display: flex; align-items: center; gap: 12px;',
    '  padding: 15px 14px 15px 18px; background: var(--accent); color: #fff;',
    '}',
    '.head h1 { margin: 0; font-size: 15px; font-weight: 650; letter-spacing: .1px; }',
    '.head p  { margin: 2px 0 0; font-size: 12px; opacity: .82; }',
    '.head .grow { flex: 1; min-width: 0; }',
    '.iconbtn {',
    '  width: 32px; height: 32px; display: grid; place-items: center;',
    '  border: 0; border-radius: 8px; background: rgba(255,255,255,.14);',
    '  color: #fff; cursor: pointer;',
    '}',
    '.iconbtn:hover { background: rgba(255,255,255,.26); }',
    '.iconbtn:focus-visible { outline: 2px solid #fff; outline-offset: 2px; }',
    '.iconbtn svg { width: 16px; height: 16px; }',

    /* Barra de fase: dice en qué paso va la persona */
    '.steps { display: flex; gap: 2px; background: var(--accent); padding: 0 18px 12px; }',
    '.steps span { flex: 1; height: 3px; border-radius: 2px; background: rgba(255,255,255,.28); }',
    '.steps span[data-on="1"] { background: #fff; }',

    /* --- Conversación --- */
    '.stream {',
    '  flex: 1; overflow-y: auto; padding: 18px 16px 8px;',
    '  background: var(--canvas); scroll-behavior: smooth;',
    '}',
    '.msg { max-width: 84%; margin-bottom: 12px; font-size: 14px; }',
    '.msg .bubble { padding: 10px 13px; border-radius: 12px; white-space: pre-wrap; word-wrap: break-word; }',
    '.msg[data-from="bot"] .bubble { background: var(--surface); border: 1px solid var(--line); border-bottom-left-radius: 4px; }',
    '.msg[data-from="me"] { margin-left: auto; }',
    '.msg[data-from="me"] .bubble { background: var(--accent); color: #fff; border-bottom-right-radius: 4px; }',
    '.msg[data-tone="warn"] .bubble { background: #FDF3F1; border-color: #F0D2CB; color: var(--danger); }',

    '.src { margin: 6px 0 0; font: 500 11px/1.4 var(--mono); color: var(--ink-2); letter-spacing: .2px; }',

    /* Confirmación sí/no */
    '.confirm { display: flex; gap: 8px; margin: 2px 0 14px; }',
    '.confirm button {',
    '  flex: 1; padding: 9px 12px; border-radius: 9px; cursor: pointer;',
    '  font: 600 13px var(--sans); background: var(--surface);',
    '  border: 1px solid var(--line); color: var(--ink);',
    '}',
    '.confirm button:hover { border-color: var(--accent); color: var(--accent); }',
    '.confirm button:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }',

    /* Escribiendo… */
    '.dots { display: inline-flex; gap: 4px; padding: 3px 0; }',
    '.dots i { width: 6px; height: 6px; border-radius: 50%; background: var(--ink-2); opacity: .45; animation: blink 1.1s infinite; }',
    '.dots i:nth-child(2) { animation-delay: .18s; } .dots i:nth-child(3) { animation-delay: .36s; }',
    '@keyframes blink { 0%,60%,100% { opacity: .25; } 30% { opacity: .9; } }',

    /* --- Barra de escritura --- */
    '.composer { display: flex; gap: 8px; padding: 12px; border-top: 1px solid var(--line); background: var(--surface); }',
    '.composer textarea {',
    '  flex: 1; resize: none; height: 42px; max-height: 110px; padding: 11px 12px;',
    '  border: 1px solid var(--line); border-radius: 10px;',
    '  font: 400 14px/1.35 var(--sans); color: var(--ink); background: var(--surface);',
    '}',
    '.composer textarea:focus { outline: none; border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-soft); }',
    '.send {',
    '  width: 42px; height: 42px; flex: none; display: grid; place-items: center;',
    '  border: 0; border-radius: 10px; background: var(--accent); color: #fff; cursor: pointer;',
    '}',
    '.send:disabled { opacity: .4; cursor: not-allowed; }',
    '.send:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }',
    '.send svg { width: 18px; height: 18px; }',

    /* --- Formulario --- */
    '.form { flex: 1; overflow-y: auto; padding: 18px 18px 20px; background: var(--surface); }',
    '.form .lead { margin: 0 0 16px; font-size: 13px; color: var(--ink-2); }',
    '.field { margin-bottom: 13px; }',
    '.field label { display: block; margin-bottom: 5px; font: 600 11px var(--mono); letter-spacing: .6px; text-transform: uppercase; color: var(--ink-2); }',
    '.field input, .field textarea {',
    '  width: 100%; padding: 10px 12px; border: 1px solid var(--line); border-radius: 9px;',
    '  font: 400 14px/1.4 var(--sans); color: var(--ink); background: var(--surface);',
    '}',
    '.field textarea { min-height: 96px; resize: vertical; }',
    '.field input:focus, .field textarea:focus { outline: none; border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-soft); }',
    '.field[data-invalid="1"] input, .field[data-invalid="1"] textarea { border-color: var(--danger); }',
    '.hint { margin: 5px 0 0; font-size: 12px; color: var(--danger); min-height: 0; }',
    '.submit {',
    '  width: 100%; margin-top: 6px; padding: 13px; border: 0; border-radius: 10px;',
    '  background: var(--accent); color: #fff; cursor: pointer; font: 650 14px var(--sans);',
    '}',
    '.submit:disabled { opacity: .55; cursor: progress; }',
    '.submit:focus-visible { outline: 3px solid var(--accent); outline-offset: 2px; }',
    '.back { margin-top: 10px; width: 100%; padding: 9px; background: none; border: 0; cursor: pointer; font: 500 13px var(--sans); color: var(--ink-2); }',
    '.back:hover { color: var(--accent); }',
    '.formerror { margin: 0 0 14px; padding: 10px 12px; border-radius: 9px; background: #FDF3F1; border: 1px solid #F0D2CB; color: var(--danger); font-size: 13px; }',

    /* --- Comprobante de radicación (elemento distintivo) --- */
    '.done { flex: 1; display: flex; flex-direction: column; justify-content: center; padding: 24px; background: var(--canvas); }',
    '.stub { background: var(--surface); border: 1px solid var(--line); border-radius: 12px; overflow: hidden; }',
    '.stub header { padding: 16px 18px 14px; border-bottom: 1px dashed var(--line); }',
    '.stub header b { display: block; font-size: 15px; }',
    '.stub header span { display: block; margin-top: 3px; font-size: 13px; color: var(--ink-2); }',
    '.stub .num { padding: 16px 18px; text-align: center; border-bottom: 1px dashed var(--line); }',
    '.stub .num small { display: block; font: 600 10px var(--mono); letter-spacing: 1.1px; text-transform: uppercase; color: var(--ink-2); }',
    '.stub .num strong { display: block; margin-top: 6px; font: 700 22px var(--mono); letter-spacing: 1px; color: var(--accent); }',
    '.stub dl { display: grid; grid-template-columns: auto 1fr; gap: 8px 14px; margin: 0; padding: 14px 18px 16px; font-size: 13px; }',
    '.stub dt { font: 600 11px var(--mono); letter-spacing: .5px; text-transform: uppercase; color: var(--ink-2); align-self: center; }',
    '.stub dd { margin: 0; text-align: right; font-weight: 600; }',
    '.done .again { margin-top: 16px; width: 100%; padding: 11px; border: 1px solid var(--line); border-radius: 10px; background: var(--surface); cursor: pointer; font: 600 13px var(--sans); color: var(--ink); }',
    '.done .again:hover { border-color: var(--accent); color: var(--accent); }',

    '.sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }',

    /* --- Responsive --- */
    '@media (max-width: 480px) {',
    '  .root { bottom: 12px; left: 12px; right: 12px; }',
    '  .panel { width: auto; height: calc(100vh - 24px); }',
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
    close: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M18 6 6 18M6 6l12 12"/></svg>',
    send: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m22 2-7 20-4-9-9-4 20-7z"/></svg>'
  };

  var HTML =
    '<div class="root" data-position="' + CFG.position + '" data-open="false">' +
      '<button class="launcher" type="button" part="launcher" aria-haspopup="dialog">' +
        ICON.chat + '<span>' + esc(CFG.title) + '</span>' +
      '</button>' +

      '<section class="panel" role="dialog" aria-modal="false" aria-label="' + esc(CFG.title) + '">' +
        '<div class="head">' +
          '<div class="grow"><h1>' + esc(CFG.title) + '</h1><p class="sub">' + esc(CFG.subtitle) + '</p></div>' +
          '<button class="iconbtn js-close" type="button" aria-label="Cerrar">' + ICON.close + '</button>' +
        '</div>' +
        '<div class="steps" aria-hidden="true"><span data-on="1"></span><span></span></div>' +

        /* Fase 1 — chat RAG */
        '<div class="stream js-stream" role="log" aria-live="polite"></div>' +
        '<div class="composer js-composer">' +
          '<label class="sr" for="pqrs-q">Escribe tu consulta</label>' +
          '<textarea id="pqrs-q" class="js-input" rows="1" placeholder="Cuéntanos tu consulta…"></textarea>' +
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
  var steps = shadow.querySelectorAll('.steps span');

  /* ------------------------------------------------------------------ *
   * 6. Utilidades
   * ------------------------------------------------------------------ */
  function esc(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }
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
      '<button type="button" data-a="no">No, quiero radicar</button>';
    box.addEventListener('click', function (e) {
      var b = e.target.closest('button');
      if (!b) return;
      box.remove();
      say('me', b.getAttribute('data-a') === 'si' ? 'Sí, resolvió mi duda' : 'No, quiero radicar');
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
      phase === 'form' ? 'Paso 2 de 2 · Radicación' :
      phase === 'done' ? 'Solicitud registrada' : CFG.subtitle;
  }

  /* ------------------------------------------------------------------ *
   * 7. Fase 1 — consulta RAG
   * ------------------------------------------------------------------ */
  function greet() {
    stream.innerHTML = '';
    say('bot', 'Hola. Pregúntame lo que necesites: si ya tenemos la respuesta, te la doy aquí mismo. Si no, radicamos tu PQRS.');
  }

  function ask() {
    var q = input.value.trim();
    if (!q || S.busy) return;

    say('me', q);
    input.value = '';
    input.style.height = '42px';
    S.lastQuery = q;
    S.busy = true;
    sendBtn.disabled = true;
    var t = typing();

    api(ENDPOINTS.ragSearch, { query: q })
      .then(function (res) {
        t.remove();
        S.interactionId = res.interactionId || null;

        if (res.answered && res.answer) {
          say('bot', res.answer);
          if (res.sources && res.sources.length) {
            var s = document.createElement('p');
            s.className = 'src';
            s.textContent = 'Basado en: ' + res.sources.slice(0, 3).join(' · ');
            stream.lastChild.appendChild(s);
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
    input.style.height = '42px';
    input.style.height = Math.min(input.scrollHeight, 110) + 'px';
  });
  shadow.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && S.open) toggle(false);
  });

  /* API pública mínima, por si el sitio quiere abrirlo desde un botón propio */
  window.PQRSWidget = {
    open: function () { toggle(true); },
    close: function () { toggle(false); },
    reset: reset
  };
})();
