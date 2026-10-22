import assert from 'node:assert/strict';

export async function assertChart(page, office = false) {
  const failures = await page.evaluate(office => {
    const failures = [], near = (a, b) => Math.abs(a - b) < 0.01;
    const check = (condition, message) => { if (!condition) failures.push(message); };
    const job = state.view.project.job;
    const actual = result?.lines.actual ?? state.view.reportedLines;
    const previous = office ? result?.previousRun?.lines.actual ?? state.view.previousRun : result?.previousRun?.lines.actual;
    const maxDay = office ? Math.max(state.view.promisedDay + 3, state.displayDay,
      ...[...Object.values(actual).flat(), ...Object.values(previous ?? {}).flat()].map(p => Math.ceil(p.t + 1))) : state.view.budget.deadlineDay;
    const left = office ? 65 : 58, bottom = office ? 524 : 425, width = office ? 850 : 660, height = office ? 486 : 380;
    const x = t => left + t / (maxDay - 1) * width, y = ft => bottom - ft / job.lengthFt * height;
    const colors = { exc: '#E0A800', pipe: '#2E86C1', back: '#D35400' };
    const marks = [...document.querySelectorAll('.station-label')];
    const lastStation = Math.floor(job.lengthFt / 100) + '+' + String(job.lengthFt % 100).padStart(2, '0');
    check(marks[0]?.textContent === '0+00' && marks.at(-1)?.textContent === lastStation, 'station endpoints');
    check(Number(marks[0]?.getAttribute('y')) > Number(marks.at(-1)?.getAttribute('y')), 'stations increase upward');
    check(marks.every(mark => Number(mark.getAttribute('x')) < left), 'station marks at left');
    const days = [...document.querySelectorAll('.day-label')];
    check(days[0]?.textContent === '1' && near(Number(days[0]?.getAttribute('x')), left), 'Day 1 at left');
    check(days.every((mark, i) => Number(mark.getAttribute('y')) > bottom
      && (!i || Number(mark.getAttribute('x')) > Number(days[i - 1].getAttribute('x')))), 'days increase rightward along bottom');
    check([...document.querySelectorAll('#chart text')].some(t => t.textContent === 'Day' && Number(t.getAttribute('y')) > bottom), 'Day axis label');
    const bands = [...document.querySelectorAll('.zone-band')];
    check(bands.length === job.zones.length, 'zone count');
    bands.forEach((band, i) => {
      const zone = job.zones[i];
      check(near(Number(band.getAttribute('x')), left) && near(Number(band.getAttribute('width')), width)
        && near(Number(band.getAttribute('y')), y(zone.toFt))
        && near(Number(band.getAttribute('height')), y(zone.fromFt) - y(zone.toFt)), 'horizontal zone ' + zone.id);
    });
    for (const line of document.querySelectorAll('#chart polyline')) {
      const id = line.dataset.line, kind = line.dataset.kind;
      const planned = result?.lines.planned ?? state.view.plannedPolylines;
      const isPlan = kind === 'plan' || kind === 'previous' && line.hasAttribute('stroke-dasharray');
      const source = kind === 'previous' ? isPlan ? result.previousRun.lines.planned[id] : previous[id] : isPlan ? planned[id] : actual[id];
      const plotted = !office && !isPlan ? source.flatMap((p, i) => i ? [{ t: p.t, ft: source[i - 1].ft }, p] : [p]) : source;
      const points = [...line.points];
      check(points.every(p => plotted.some(q => near(p.x, x(q.t)) && near(p.y, y(q.ft)))), `${kind} ${id} maps time to x and station to y`);
      check(line.getAttribute('stroke') === colors[id], `${kind} ${id} crew ink`);
      check(isPlan === line.hasAttribute('stroke-dasharray'), `${kind} ${id} dash style`);
    }
    const ruler = document.querySelector('#ruler');
    check(ruler.querySelector('path').getAttribute('d') === (office ? 'M0 38V524' : 'M0 45V425'), 'vertical day ruler');
    // Inline target avoids reading an intermediate animation frame.
    const translate = office ? Number(ruler.style.transform.match(/translateX\(([^p]+)px\)/)?.[1])
      : Number(ruler.getAttribute('transform').match(/translate\(([^ ]+) 0\)/)?.[1]);
    check(near(translate, x(state.displayDay - 1)), 'ruler follows current day rightward');
    return failures;
  }, office);
  assert.deepEqual(failures, []);
}
