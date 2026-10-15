# Check the manual's front-page rule against a fine-step simulation:
# an event that drops the hit crew's rate by r for d days costs it r*d days of output;
# the crew behind it (same base rate, starting b days later) is blocked iff b < r*d.
def blocked_days(b, r, d, t0=2.0, L=12.0, dt=0.001):
    up = dn = 0.0; t = 0.0; blocked = 0.0
    while dn < L:
        ur = 0.0 if up >= L else (1.0 - r if t0 <= t < t0 + d else 1.0)
        up = min(L, up + ur * dt)
        if t >= b:
            want = dn + dt
            if want > up and up < L:     # cannot pass the crew ahead
                blocked += dt
                dn = min(dn, up)
            else:
                dn = min(L, want)
        t += dt
    return round(blocked, 2)

events = {'E1 utility strike (40% x 3 d)': (0.4, 3), 'E2 rock (60% x 2 d)': (0.6, 2),
          'P1 collapse, wait (100% x 3 d)': (1.0, 3), 'P2 late pipe (50% x 3 d)': (0.5, 3),
          'P3 dropped pipe (80% x 2 d)': (0.8, 2)}
print(f"{'event':<34}{'r*d':>6}   crew-behind idle days at buffer b = 0.5 / 1 / 1.5 / 2 / 3 / 4")
for name, (r, d) in events.items():
    row = [blocked_days(b, r, d) for b in (0.5, 1, 1.5, 2, 3, 4)]
    print(f"{name:<34}{r*d:>6.1f}   {row}")
