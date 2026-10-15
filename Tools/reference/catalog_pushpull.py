# Push vs pull with LOB-Game's default fleet (one Standard machine per crew):
# excavator 220 ft/day $1,200/day, crane 180 ft/day $1,800/day, backfill 250 ft/day $1,800/day,
# overhead 30% of direct spend, idle machines still bill (per the LOB-Game docs digest).
L = 6000.0
RATE = {'E': 220.0, 'P': 180.0, 'B': 250.0}
COST = {'E': 1200.0, 'P': 1800.0, 'B': 1800.0}
DT = 0.01

def run(start, cap=None, label=''):
    pos = {c: 0.0 for c in 'EPB'}
    work = {c: 0.0 for c in 'EPB'}
    done = {c: None for c in 'EPB'}
    t, max_open = 0.0, 0.0
    while done['B'] is None and t < 200:
        for c in 'EPB':
            if done[c] is not None or t < start[c]:
                continue
            step = RATE[c] * DT
            if c == 'E':
                limit = L
                if cap is not None:                      # WIP cap: open trench = dug but not backfilled
                    limit = min(limit, pos['B'] + cap)
            elif c == 'P':
                limit = pos['E']
            else:
                limit = pos['P']
            new = min(pos[c] + step, limit)
            if new > pos[c] + 1e-9:
                work[c] += (new - pos[c]) / RATE[c]
            pos[c] = new
            if pos[c] >= L - 1e-6:
                done[c] = t + DT
        max_open = max(max_open, pos['E'] - pos['B'])
        t += DT
    onsite = {c: done[c] - start[c] for c in 'EPB'}
    idle = {c: onsite[c] - work[c] for c in 'EPB'}
    cost = sum(COST[c] * onsite[c] for c in 'EPB') * 1.3
    print(f"{label:<40} start E/P/B day {start['E']}/{start['P']}/{start['B']}  cap {'none' if cap is None else int(cap)} ft"
          f" | finish day {done['B']:.1f} | idle E/P/B {idle['E']:.1f}/{idle['P']:.1f}/{idle['B']:.1f} d"
          f" | crew cost ${cost:,.0f} | peak open trench {max_open:,.0f} ft")

print("Durations alone: E %.1f d, P %.1f d, B %.1f d" % (L/RATE['E'], L/RATE['P'], L/RATE['B']))
run({'E': 0, 'P': 1, 'B': 2}, None, "Push (everyone as early as possible)")
run({'E': 0, 'P': 1, 'B': 11}, None, "Pull: backfill held (time buffer)")
run({'E': 0, 'P': 1, 'B': 11}, 2000, "Pull: + dig released within 2,000 ft")
run({'E': 0, 'P': 1, 'B': 11}, 1500, "Pull: + dig released within 1,500 ft")
run({'E': 4, 'P': 5, 'B': 15}, None, "Pull: whole job shifted 4 days later")
