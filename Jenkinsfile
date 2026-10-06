pipeline {
    agent{
        docker{
            image '23107560/node16-docker:latest'
            // Connect to the DinD container trough TLS certificate
            args '''
                --network jenkins_dind
                --add-host=docker:host-gateway
                -v /certs/client:/certs/client:ro
                -e DOCKER_HOST=tcp://docker:2376
                -e DOCKER_CERT_PATH=/certs/client
                -e DOCKER_TLS_VERIFY=1
            '''
        }
    }
    environment {
        DOCKER_HOST = 'tcp://docker:2376'
        DOCKER_CERT_PATH = '/certs/client'
        DOCKER_TLS_VERIFY = '1'
    }

    stages {

        stage('Verify Docker') {
            steps {
                sh '''
                    set -e

                    echo "Docker version:"
                    docker --version

                    echo ""
                    echo "Docker info:"
                    docker info

                    echo ""
                    echo "Workspace:"
                    pwd
                    ls -la
                '''
            }
        }

        stage('Secret scan - Gitleaks') {
            steps {
                sh '''
                    set -e

                    echo "Running Gitleaks..."

                    docker run --rm \
                        -v "$WORKSPACE:/repo" \
                        -w /repo \
                        zricethezav/gitleaks:latest \
                        detect \
                        --source . \
                        --verbose \
                        --report-format json \
                        --report-path gitleaks-report.json
                '''
            }

            post {
                always {
                    archiveArtifacts(
                        artifacts: 'gitleaks-report.json',
                        allowEmptyArchive: true
                    )
                }
            }
        }

        stage('Dependency scan - frontend') {
            steps {
                sh '''
                    set -e

                    echo "Running npm dependency scan..."

                    docker run --rm \
                        --network host \
                        -v "$WORKSPACE/tutor-studio:/app" \
                        -w /app \
                        node:22 \
                        sh -c '
                            npm ci --verbose
                            npm audit --json > npm-audit-report.json || true
                            npm audit --audit-level=high
                        '
                '''
            }

            post {
                always {
                    archiveArtifacts(
                        artifacts: 'tutor-studio/npm-audit-report.json',
                        allowEmptyArchive: true
                    )
                }
            }
        }

        stage('Dependency scan - backend') {
            steps {
                sh '''
                    set -e

                    echo "Running .NET dependency scan..."

                    docker run --rm \
                        --network host \
                        -v "$WORKSPACE:/src" \
                        -w /src \
                        mcr.microsoft.com/dotnet/sdk:10.0 \
                        sh -c '
                            dotnet restore bitcraft.slnx

                            OUTPUT=$(dotnet list bitcraft.slnx package --vulnerable --include-transitive || true)

                            echo "$OUTPUT"

                            echo "$OUTPUT" > dotnet-vulnerable-report.txt

                            if echo "$OUTPUT" | grep -qi "has the following vulnerable packages"; then
                                echo "Vulnerable packages found - failing build."
                                exit 1
                            fi
                        '
                '''
            }

            post {
                always {
                    archiveArtifacts(
                        artifacts: 'dotnet-vulnerable-report.txt',
                        allowEmptyArchive: true
                    )
                }
            }
        }

        stage('Test - frontend') {
            steps {
                sh '''
                    set -e

                    echo "Running frontend tests..."

                    docker run --rm \
                        --network host \
                        -v "$WORKSPACE/tutor-studio:/app" \
                        -w /app \
                        node:20 \
                        sh -c '
                            npm ci
                            mkdir -p test-results
                            TZ=Australia/Perth npx vitest run \
                                --reporter=default \
                                --outputFile=test-results/junit.xml \
                                --maxWorkers=1
                        '
                '''
            }

            post {
                always {
                    junit(
                        testResults: 'tutor-studio/test-results/junit.xml',
                        allowEmptyResults: true
                    )
                }
            }
        }

        stage('Test - backend') {
            steps {
                sh '''
                    set -e

                    echo "Running backend tests..."

                    docker run --rm \
                        --network host \
                        -v "$WORKSPACE:/src" \
                        -w /src \
                        mcr.microsoft.com/dotnet/sdk:10.0 \
                        sh -c '
                            dotnet restore bitcraft.slnx

                            dotnet add tests/Bitcraft.UnitTests package JUnitTestLogger --version 1.1.0
                            dotnet add tests/Bitcraft.ApiIntegrationTests package JUnitTestLogger --version 1.1.0
                            dotnet add tests/Bitcraft.InfrastructureIntegrationTests package JUnitTestLogger --version 1.1.0

                            mkdir -p test-results

                            dotnet test bitcraft.slnx \
                                --logger "console;verbosity=normal" \
                                --logger "junit;LogFilePath=/src/test-results/{assembly}.xml"
                        '
                '''
            }

            post {
                always {
                    junit(
                        testResults: 'test-results/*.xml',
                        allowEmptyResults: true
                    )
                }
            }
        }

        stage('Approve deploy') {
            steps {
                input(
                    message: 'Deploy Bitcraft to Jetson?',
                    ok: 'Deploy'
                )
            }
        }

        stage('Deploy Bitcraft to Jetson') {
            steps {
                withCredentials([
                    sshUserPrivateKey(
                        credentialsId: 'jetson-ssh-key',
                        keyFileVariable: 'JETSON_SSH_KEY',
                        usernameVariable: 'JETSON_USER'
                    ),
                    string(
                        credentialsId: 'jetson-host',
                        variable: 'JETSON_HOST'
                    ),
                    string(
                        credentialsId: 'jetson-host-key',
                        variable: 'JETSON_HOST_KEY'
                    )
                ]) {
                    sh '''
                        set -euo pipefail

                        mkdir -p ~/.ssh
                        chmod 700 ~/.ssh

                        if ! grep -qxF "$JETSON_HOST_KEY" ~/.ssh/known_hosts 2>/dev/null; then
                            printf "%s\\n" "$JETSON_HOST_KEY" >> ~/.ssh/known_hosts
                        fi

                        chmod 600 ~/.ssh/known_hosts

                        export DEPLOY_SHA="$GIT_COMMIT"

                        echo "Deploying commit: $DEPLOY_SHA"

                        ssh \
                            -i "$JETSON_SSH_KEY" \
                            -o BatchMode=yes \
                            "$JETSON_USER@$JETSON_HOST" \
                            "bash -se" <<REMOTE | tee deploy.log

                        set -euo pipefail

                        cd /home/cmpe6012/AgenticTutor/bitcraft-deploy

                        echo "Fetching deploy branch..."
                        git fetch origin deploy/orin

                        echo "Deploying pinned commit: $DEPLOY_SHA"
                        git reset --hard "$DEPLOY_SHA"

                        echo "Current commit:"
                        git log -1 --oneline

                        echo "Starting Docker deployment..."
                        sudo -n /usr/local/sbin/bitcraft-deploy

                        echo "BITCRAFT_DEPLOYMENT_SUCCESS"

REMOTE
                    '''
                }
            }

            post {
                always {
                    archiveArtifacts(
                        artifacts: 'deploy.log',
                        allowEmptyArchive: true
                    )
                }
            }
        }
    }

    post {
        success {
            echo 'Bitcraft pipeline completed successfully.'
        }

        failure {
            echo 'Bitcraft pipeline failed. Check the stage logs.'
        }

        always {
            echo 'Bitcraft CI/CD pipeline completed.'
        }
    }
}
