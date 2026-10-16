
                    set -e

                    echo "Running frontend tests..."

                    docker run --rm                         --network host                         -v "$WORKSPACE/tutor-studio:/app"                         -w /app                         node:20                         sh -c '
                            npm ci
                            mkdir -p test-results
                            npx vitest run tests/DocumentCreateDialog.test.tsx --reporter=verbose
                            TZ=Australia/Perth npx vitest run                                 --reporter=default                                 --outputFile=test-results/junit.xml                                 --maxWorkers=1
                        '
                