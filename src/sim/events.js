import { CLEARING_DAY_STEP, EPS, emit, protectionAt } from './common.js';
import { eventDeviations } from './deviations.js';
import { finishRain } from './rain.js';

export function clearingDays(s, event, machine) {
  const size = s.catalog.machines[machine].sizeClass;
  const severity = event.protection ? event.severityByProtection[event.protection] ?? 1 : 1;
  return Math.round(event.durationDaysByExcavator[size] * severity / CLEARING_DAY_STEP) * CLEARING_DAY_STEP;
}

export function availableResponses(s) {
  return s.events.flatMap(event => (event.fired && !event.resolved
    || event.forecast && s.t >= event.forecast.inboxDay - 1 && !event.resolved)
    ? (event.options ?? []).map(option => ({ event, option })) : []);
}

export function updateEvents(s) {
  for (const event of s.events) {
    if (event.forecast && !event.forecastSent && s.t >= event.forecast.inboxDay - 1 - EPS) {
      event.forecastSent = true;
      s.inbox.push({ day: event.forecast.inboxDay - 1, from: 'Forecast', subject: 'Weather', body: event.forecast.text });
    }
    if (!event.fired) {
      const trigger = event.trigger;
      const ready = trigger.kind === 'day' ? s.t >= trigger.day - 1 - EPS
        : trigger.kind === 'crewReaches' ? s.crews[trigger.crew].pos >= trigger.ft - EPS
          : trigger.kind === 'zoneSigned' && s.permits[trigger.zone].signed;
      if (!ready) continue;
      event.fired = true;
      event.firedT = s.t;
      event.protection = protectionAt(s, event.stationFt)?.kind ?? null;
      event.shownCard = event.protection && event.cardWhenProtected ? event.cardWhenProtected : event.card;
      event.endsT = event.type === 'P1' ? null : s.t + event.durationDays;
      if (event.type === 'A2') event.openFt = Math.max(0, s.crews.exc.pos - s.crews.back.pos);
      s.speed = 'slow';
      emit(s, 'event', { eventId: event.id, day: s.t, zone: event.zone, stationFt: event.stationFt,
        card: event.shownCard, seats: event.visibleTo });
      eventDeviations(s, event);
    }
    if (event.resolved || event.endsT == null || s.t < event.endsT - EPS) continue;
    if (event.type === 'A2') {
      if (!event.rainEnded) finishRain(s, event);
      if (s.t < event.reworkEndsT - EPS) continue;
    }
    event.resolved = true;
    event.resolvedT = s.t;
  }
  s.scenario.inbox.forEach((item, index) => {
    if (!s.inboxDelivered.includes(index) && s.t >= item.day - 1 - EPS) {
      s.inboxDelivered.push(index);
      s.inbox.push({ ...item, day: item.day - 1 });
    }
  });
}
