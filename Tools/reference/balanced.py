# Same model as catalog_pushpull.py, but crews can swap to the catalog's Small machines.
L = 6000.0
DT = 0.01
FLEET = {  # (ft/day, $/day) from the LOB-Game catalog digest
    'E_std': (220.0, 1200.0), 'E_small': (165.0, 900.0),
    'P_std': (180.0, 1800.0),
    'B_std': (250.0, 1800.0), 'B_small': (180.0, 1400.0),
}

def run(label, start, mach, cap=None):
    rate = {c: FLEET[mach[c]][0] for c in 'EPB'}
    cost_day = {c: FLEET[mach[c]][1] for c in 'EPB'}
    pos = {c: 0.0 for c in 'EPB'}; work = {c: 0.0 for c in 'EPB'}; done = {c: None for c in 'EPB'}
    t = 0.0; peak = 0.0
    while done['B'] is None and t < 300:
        for c in 'EPB':
            if done[c] is not None or t < start[c]:
                continue
            limit = L if c == 'E' else pos['E'] if c == 'P' else pos['P']
            if c == 'E' and cap is not None:
                limit = min(limit, pos['B'] + cap)
            new = min(pos[c] + rate[c] * DT, limit)
            if new > pos[c] + 1e-9:
                work[c] += (new - pos[c]) / rate[c]
            pos[c] = new
            if pos[c] >= L - 1e-6:
                done[c] = t + DT
        peak = max(peak, pos['E'] - pos['B'])
        t += DT
    onsite = {c: done[c] - start[c] for c in 'EPB'}
    idle = {c: onsite[c] - work[c] for c in 'EPB'}
    cost = sum(cost_day[c] * onsite[c] for c in 'EPB') * 1.3
    print(f"{label:<44} finish day {done['B']:.1f} | idle E/P/B {idle['E']:.1f}/{idle['P']:.1f}/{idle['B']:.1f} d"
          f" | crew cost ${cost:,.0f} | peak open trench {peak:,.0f} ft")

std = {'E': 'E_std', 'P': 'P_std', 'B': 'B_std'}
bal = {'E': 'E_std', 'P': 'P_std', 'B': 'B_small'}
run("Push: std fleet, start 0/1/2", {'E': 0, 'P': 1, 'B': 2}, std)
run("Pull A: std fleet, backfill held to day 11", {'E': 0, 'P': 1, 'B': 11}, std)
run("Pull B: small backfill (180) at day 2", {'E': 0, 'P': 1, 'B': 2}, bal)
run("Pull C: small backfill + dig within 1,000 ft", {'E': 0, 'P': 1, 'B': 2}, bal, cap=1000)
run("Pull D: small backfill + dig within 600 ft", {'E': 0, 'P': 1, 'B': 2}, bal, cap=600)
