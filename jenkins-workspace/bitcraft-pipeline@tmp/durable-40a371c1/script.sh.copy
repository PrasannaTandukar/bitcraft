
                    set -e

                    echo "Running .NET dependency scan..."

                    docker run --rm                         -v "$WORKSPACE:/src"                         -w /src                         mcr.microsoft.com/dotnet/sdk:10.0                         sh -c '
                            dotnet restore bitcraft.slnx

                            OUTPUT=$(dotnet list bitcraft.slnx package --vulnerable --include-transitive || true)

                            echo "$OUTPUT"

                            echo "$OUTPUT" > dotnet-vulnerable-report.txt

                            if echo "$OUTPUT" | grep -qi "has the following vulnerable packages"; then
                                echo "Vulnerable packages found - failing build."
                                exit 1
                            fi
                        '
                