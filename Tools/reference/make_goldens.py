"""Reference oracle for the no-event push/pull goldens (Phase 0, WO-02).
Continuous-time crew model, DT = 0.01 day. Conventions the JS and C# simulations must reproduce:
- Day N in the design means the crew starts at t = N - 1 (Day 1 = t 0). Output 'finishDay' is t at completion.
- A crew advances at its machine rate, never past its leader (pipe <= excavation, backfill <= pipe), no distance margin.
- Optional WIP cap: excavation may not lead backfill by more than cap ft (pull by distance).
- idleDays = days on site - days actually working; on site = from start to that crew's own finish.
- crewCostUsd = sum(daily machine cost x days on site) x 1.3 overhead; mobilization, events and purchases excluded.
- peakOpenTrenchFt = max over time of (excavation position - backfill position).
Tolerances for parity: finish +-0.1 day, idle +-0.1 day, cost +-$500, peak +-10 ft.
"""
import json, sys
L = 6000.0; DT = 0.01
FLEET = {'excavator_standard': (220.0, 1200.0), 'excavator_small': (165.0, 900.0), 'excavator_large': (330.0, 1800.0),
         'crane_standard': (180.0, 1800.0), 'crane_heavy': (270.0, 2800.0),
         'backfill_standard': (250.0, 1800.0), 'backfill_small': (180.0, 1400.0), 'backfill_large': (375.0, 2600.0)}
ORDER = ['exc', 'pipe', 'back']

def run(start_t, mach, cap=None):
    rate = {c: FLEET[mach[c]][0] for c in ORDER}; cost_day = {c: FLEET[mach[c]][1] for c in ORDER}
    pos = {c: 0.0 for c in ORDER}; work = {c: 0.0 for c in ORDER}; done = {c: None for c in ORDER}
    t = 0.0; peak = 0.0
    while done['back'] is None and t < 300:
        for c in ORDER:
            if done[c] is not None or t < start_t[c]: continue
            limit = L if c == 'exc' else pos['exc'] if c == 'pipe' else pos['pipe']
            if c == 'exc' and cap is not None: limit = min(limit, pos['back'] + cap)
            new = min(pos[c] + rate[c] * DT, limit)
            if new > pos[c] + 1e-9: work[c] += (new - pos[c]) / rate[c]
            pos[c] = new
            if pos[c] >= L - 1e-6: done[c] = t + DT
        peak = max(peak, pos['exc'] - pos['back']); t += DT
    onsite = {c: done[c] - start_t[c] for c in ORDER}
    idle = {c: round(onsite[c] - work[c], 1) for c in ORDER}
    cost = round(sum(cost_day[c] * onsite[c] for c in ORDER) * 1.3)
    return {'finishDay': round(done['back'], 1), 'idleDays': idle, 'crewCostUsd': cost, 'peakOpenTrenchFt': round(peak)}

STD = {'exc': 'excavator_standard', 'pipe': 'crane_standard', 'back': 'backfill_standard'}
SMALL_B = {'exc': 'excavator_standard', 'pipe': 'crane_standard', 'back': 'backfill_small'}
cases = {
 'push_1_2_3':        dict(startDay={'exc': 1, 'pipe': 2, 'back': 3},  machines=STD,     capFt=None),
 'pm_plan_1_2_5':     dict(startDay={'exc': 1, 'pipe': 2, 'back': 5},  machines=STD,     capFt=None),
 'pull_time_1_2_12':  dict(startDay={'exc': 1, 'pipe': 2, 'back': 12}, machines=STD,     capFt=None),
 'pull_slope_small_backfill': dict(startDay={'exc': 1, 'pipe': 2, 'back': 3}, machines=SMALL_B, capFt=None),
 'pull_distance_1000_small_backfill': dict(startDay={'exc': 1, 'pipe': 2, 'back': 3}, machines=SMALL_B, capFt=1000),
}
out = {'_doc': __doc__.strip().splitlines(), 'lengthFt': L, 'dt': DT, 'cases': {}}
for name, c in cases.items():
    res = run({k: v - 1 for k, v in c['startDay'].items()}, c['machines'], c['capFt'])
    out['cases'][name] = {'input': c, 'expected': res}
    print(f"{name:<36} finish {res['finishDay']:>5} | idle {res['idleDays']} | cost ${res['crewCostUsd']:,} | peak {res['peakOpenTrenchFt']} ft")
path = sys.argv[1] if len(sys.argv) > 1 else 'no_events.json'
json.dump(out, open(path, 'w'), indent=1)
print('wrote', path)
