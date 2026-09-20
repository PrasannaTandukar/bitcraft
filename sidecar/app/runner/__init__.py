"""Per-run benchmark runner package.

The dispatcher in `app.tools.deepeval_llm_benchmarks` spawns
`python -m app.runner` as one subprocess per standardized benchmark run
(plan.standard-benchmark-hardening WS1, decisions D-R1/D-R2). The subprocess
executes the synchronous deepeval evaluate() call off the uvicorn event loop
and reports progress through a JSON status file.
"""
