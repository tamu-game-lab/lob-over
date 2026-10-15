# Monitor
Node 20+ ES modules; run the commands below from the repository root.
`src/common/`: seeded RNG, catalog access, units, and stderr logger.
`makeRng(seed)` uses uint32 seeds; `int(lo, hi)` includes both bounds; resume with `makeRng(rng.state())`.
`loadCatalog(path?)` returns parsed data; named `machine`, `protection`, and `city` helpers use the default catalog.
`src/scenario/`, `src/sim/`, `src/host/`: validation, simulation, and host entry points.
`web/`: future browser pages; `test/`: unit tests; `e2e/`: future browser tests; `out/`: ignored output.
`npm.cmd --prefix Monitor test`: run all discovered `*.test.js` tests.
`npm.cmd --prefix Monitor run validate -- <scenario.json>`: validation stub, exits 2 until WO-01.
`npm.cmd --prefix Monitor run headless`: simulation stub, exits 2 until WO-02.
`npm.cmd --prefix Monitor run host`: host stub, exits 2 until WO-03; use this command to start the future host.
`npm.cmd --prefix Monitor run e2e`: reserved for `e2e/stage1.e2e.js` in WO-03.
Plan URL once WO-03 is implemented: http://localhost:7331/plan.html
Site URL once WO-03 is implemented: http://localhost:7331/site.html
Facilitator URL once WO-03 is implemented: http://localhost:7331/facilitator.html
