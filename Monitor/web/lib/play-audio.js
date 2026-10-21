// WO-L3: the office owns its audio so other seats keep their existing playback.
window.LobOfficeAudio = (() => {
  // Docs/LEVEL1.md: exchanges have a 0.3 second gap. UI sounds are synthesized.
  const GAP_MS = 300;
  function create(onVoice) {
    let context, master, unlocked = false, muted = false, current, playing = false;
    let generation = 0, timer, queue = [];
    function unlock() {
      unlocked = true;
      if (!context) {
        const AudioContext = window.AudioContext || window.webkitAudioContext;
        if (AudioContext) { context = new AudioContext(); master = context.createGain(); master.gain.value = muted ? 0 : .35; master.connect(context.destination); }
      }
      context?.resume().catch(() => {}); next();
    }
    document.addEventListener('pointerdown', unlock);
    document.addEventListener('keydown', unlock);
    function tone(frequency, duration, gain, delay = 0, type = 'sine', end = frequency) {
      const start = context.currentTime + delay, oscillator = context.createOscillator(), envelope = context.createGain();
      oscillator.type = type; oscillator.frequency.setValueAtTime(frequency, start);
      oscillator.frequency.exponentialRampToValueAtTime(end, start + duration);
      envelope.gain.setValueAtTime(gain, start); envelope.gain.exponentialRampToValueAtTime(.001, start + duration);
      oscillator.connect(envelope); envelope.connect(master); oscillator.start(start); oscillator.stop(start + duration);
      oscillator.onended = () => { oscillator.disconnect(); envelope.disconnect(); };
    }
    function noise(duration, gain, cutoff) {
      const buffer = context.createBuffer(1, Math.ceil(context.sampleRate * duration), context.sampleRate), samples = buffer.getChannelData(0);
      // Repeatable noise texture, used only by the UI sound generator.
      let seed = 17;
      for (let i = 0; i < samples.length; i++) { seed = (seed * 16807) % 2147483647; samples[i] = seed / 1073741824 - 1; }
      const source = context.createBufferSource(), filter = context.createBiquadFilter(), envelope = context.createGain();
      source.buffer = buffer; filter.type = 'bandpass'; filter.frequency.value = cutoff;
      envelope.gain.setValueAtTime(gain, context.currentTime); envelope.gain.exponentialRampToValueAtTime(.001, context.currentTime + duration);
      source.connect(filter); filter.connect(envelope); envelope.connect(master); source.start();
      source.onended = () => { source.disconnect(); filter.disconnect(); envelope.disconnect(); };
    }
    function sound(name) {
      if (!unlocked || muted || !context || context.state !== 'running') return;
      if (name === 'switch') { noise(.09, .9, 600); tone(110, .12, .6, 0, 'triangle', 45); tone(1400, .07, .15, .13); tone(1900, .09, .12, .22); }
      else if (name === 'hiss') noise(.45, .35, 1900);
      else if (name === 'tick') tone(1100, .035, .18, 0, 'triangle', 650);
      else if (name === 'teletype') noise(.018, .18, 2600);
      else tone(700, .035, .2, 0, 'triangle', 350);
    }
    function next() {
      if (!unlocked || playing || !queue.length) return;
      const item = queue.shift(), version = generation;
      playing = true; current = new Audio(item.url); current.muted = muted;
      let finished = false, fallback = false;
      const finish = () => {
        if (version !== generation || finished) return;
        finished = true; onVoice(''); timer = setTimeout(() => { playing = false; next(); }, GAP_MS);
      };
      const unavailable = () => {
        if (version !== generation || finished || fallback) return;
        fallback = true; onVoice(item.text);
        // Missing local voice files still leave time to read the authored line.
        timer = setTimeout(finish, Math.max(2500, item.text.split(/\s+/).length * 320));
      };
      current.onplaying = () => { if (version === generation) onVoice(item.text); };
      current.onended = finish; current.onerror = unavailable;
      current.play().catch(error => {
        if (version !== generation || finished) return;
        if (error.name === 'NotAllowedError') { queue.unshift(item); playing = false; unlocked = false; }
        else unavailable();
      });
    }
    return {
      sound,
      toggleMute() { muted = !muted; if (master) master.gain.value = muted ? 0 : .35; if (current) current.muted = muted; return muted; },
      play(message, stage) {
        const turns = message.turns ?? (message.lines ?? []).flatMap(line => line.turns ?? [{ ...line, clip: line.id + '.wav' }]);
        for (const turn of turns) queue.push({ url: `/Voice/${encodeURIComponent(stage)}/${encodeURIComponent(turn.clip)}`, text: turn.text ?? '' });
        next();
      },
      reset() { generation++; current?.pause(); current = null; clearTimeout(timer); queue = []; playing = false; onVoice(''); },
    };
  }
  return { create };
})();
