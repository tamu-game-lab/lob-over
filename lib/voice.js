// Docs/LEVEL1.md: serialize exchanges, with 0.3 seconds between turns and no captions.
window.LobVoice = (() => {
  const GAP_MS = 300;
  let queue = [], playing = false, current, timer, generation = 0, blocked = false;
  function next() {
    if (playing || blocked || !queue.length) return;
    const item = queue.shift(), version = generation;
    playing = true;
    current = new Audio(item.url);
    const ended = () => {
      if (version !== generation) return;
      timer = setTimeout(() => { playing = false; next(); }, GAP_MS);
    };
    current.onended = ended;
    current.onerror = ended;
    current.play().catch(() => {
      if (version !== generation) return;
      queue.unshift(item); playing = false; blocked = true;
    });
  }
  document.addEventListener('pointerdown', () => { blocked = false; next(); });
  document.addEventListener('keydown', () => { blocked = false; next(); });
  return {
    play(message, stage) {
      for (const turn of message.turns ?? []) queue.push({
        url: `Voice/${encodeURIComponent(stage)}/${encodeURIComponent(turn.clip)}`,
      });
      next();
    },
    reset() { generation++; current?.pause(); clearTimeout(timer); queue = []; playing = blocked = false; },
  };
})();
