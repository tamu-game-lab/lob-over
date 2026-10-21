/* WO-S7: the host supplies one prompt per seat; no guide source is loaded here. */
globalThis.LobGuide = {
  current: null,
  attach(socket, seat, onGuide = () => {}) {
    const facilitator = seat === 'facilitator';
    const style = document.createElement('style');
    style.textContent = '#guide[hidden]{display:none}#guide{margin:0;padding:18px 26px;background:#e2eaf0;border-bottom:2px solid #526b7b;color:#23313b}#guide h2{font-size:21px;margin:0 0 8px}#guide p{margin:8px 0;font-size:17px;max-width:1000px}#guide button{font:inherit;padding:9px 15px;background:#fffdf8;border:1px solid #526b7b;color:inherit;border-radius:3px}#guide button:disabled{opacity:.65}#guide-narration{font-size:26px!important;line-height:1.5}#guide-readiness{display:flex;gap:18px;flex-wrap:wrap;margin:14px 0}#guide-beats{display:flex;flex-wrap:wrap;gap:6px 28px;padding-left:22px}#guide-beats [data-status=current]{font-weight:700;text-decoration:underline}#guide-beats [data-status=skipped]{opacity:.6}#guide-prompts{white-space:pre-line}@media(max-width:600px){#guide{padding:14px}#guide p{font-size:16px}}';
    document.head.append(style);
    const banner = document.createElement('section');
    banner.id = 'guide'; banner.hidden = true; banner.setAttribute('aria-label', 'Guided playthrough');
    banner.innerHTML = '<h2 id="guide-title"></h2><p id="guide-prompt"></p>' + (facilitator
      ? '<p id="guide-narration"></p><p id="guide-prompts"></p><div id="guide-readiness"></div><button id="guide-continue">Continue</button><ol id="guide-beats"></ol>'
      : '<button id="guide-ready">Ready</button>');
    document.querySelector('header').after(banner);
    const find = id => banner.querySelector('#' + id);
    let mirror = false, sequence = 0, run;
    find(facilitator ? 'guide-continue' : 'guide-ready').onclick = () => {
      if (socket.readyState !== WebSocket.OPEN || mirror) return;
      socket.send(JSON.stringify({ t: facilitator ? 'ctrl' : 'cmd', v: 1, id: 'guide-' + (++sequence),
        ...(facilitator ? {} : { seat }), action: facilitator ? 'continue' : 'ready' }));
    };
    socket.addEventListener('message', event => {
      const message = JSON.parse(event.data);
      if (message.t === 'welcome') mirror = message.mirror;
      const state = message.t === 'welcome' ? message.state : message.t === 'state' ? message : null;
      if (state && run !== state.runId) { run = state.runId; banner.hidden = true; this.current = null; }
      if (message.t !== 'guide') return;
      this.current = message;
      banner.hidden = facilitator ? !message.beats : !message.beat;
      banner.dataset.beat = message.beat?.id ?? '';
      find('guide-title').textContent = message.beat ? `${message.beat.index + 1} / ${message.beat.count} · ${message.beat.title}` : 'Guided playthrough';
      find('guide-prompt').textContent = message.prompt ?? '';
      if (facilitator) {
        find('guide-narration').textContent = message.narration ?? '';
        find('guide-prompts').textContent = Object.entries(message.prompts ?? {}).map(([name, prompt]) => name + ': ' + prompt).join('\n');
        find('guide-readiness').replaceChildren(...['site', 'plan', 'rules'].map(name => {
          const light = document.createElement('span'); light.dataset.seat = name;
          light.textContent = `${message.ready?.[name] ? '●' : '○'} ${name} · ${message.ready?.[name] ? 'ready' : 'waiting'}`;
          return light;
        }));
        find('guide-beats').replaceChildren(...(message.beats ?? []).map(beat => {
          const item = document.createElement('li'); item.dataset.status = beat.status;
          item.textContent = `${beat.title} · ${beat.status}`; return item;
        }));
        find('guide-continue').disabled = mirror || !message.beat;
      } else {
        find('guide-ready').disabled = mirror || !message.beat || message.ready?.[seat];
        find('guide-ready').textContent = message.ready?.[seat] ? 'Waiting for the others' : 'Ready';
      }
      onGuide(message);
    });
  },
};
