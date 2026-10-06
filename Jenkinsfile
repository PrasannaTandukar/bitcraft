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

        stage('Scans') {
            parallel {

                stage('Secret scan (gitleaks)') {
                    agent {
                        docker {
                            image 'zricethezav/gitleaks:latest'
                            args '''
                                --network jenkins_dind
                                -v /certs/client:/certs/client:ro
                                -e DOCKER_HOST=tcp://docker:2376
                                -e DOCKER_CERT_PATH=/certs/client
                                -e DOCKER_TLS_VERIFY=1
                            '''
                        }
                    }

                    steps {
                        checkout([
                            $class: 'GitSCM',
                            branches: scm.branches,
                            userRemoteConfigs: scm.userRemoteConfigs,
                            extensions: [
                                [
                                    $class: 'CloneOption',
                                    shallow: false,
                                    depth: 0,
                                    noTags: false
                                ]
                            ]
                        ])

                        sh '''
                            set -e

                            gitleaks detect \
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

                stage('Dependency scan — frontend (npm audit)') {
                    agent {
                        docker {
                            image 'node:20'
                            args '''
                                --network jenkins_dind
                                -v /certs/client:/certs/client:ro
                                -e DOCKER_HOST=tcp://docker:2376
                                -e DOCKER_CERT_PATH=/certs/client
                                -e DOCKER_TLS_VERIFY=1
                            '''
                        }
                    }

                    steps {
                        dir('tutor-studio') {
                            sh '''
                                set -e

                                npm ci

                                npm audit --json > npm-audit-report.json || true

                                npm audit --audit-level=high
                            '''
                        }
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

                stage('Dependency scan — backend (.NET)') {
                    agent {
                        docker {
                            image 'mcr.microsoft.com/dotnet/sdk:10.0'
                            args '''
                                --network jenkins_dind
                                -v /certs/client:/certs/client:ro
                                -e DOCKER_HOST=tcp://docker:2376
                                -e DOCKER_CERT_PATH=/certs/client
                                -e DOCKER_TLS_VERIFY=1
                            '''
                        }
                    }

                    steps {
                        sh '''
                            set -e

                            dotnet restore bitcraft.slnx

                            OUTPUT="$(dotnet list bitcraft.slnx package --vulnerable --include-transitive)"

                            echo "$OUTPUT"

                            echo "$OUTPUT" > dotnet-vulnerable-report.txt

                            if echo "$OUTPUT" | grep -qi "has the following vulnerable packages"; then
                                echo "Vulnerable packages found — failing build."
                                exit 1
                            fi
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
            }
        }

        stage('Tests') {
            parallel {

                stage('Test — frontend (npm)') {
                    agent {
                        docker {
                            image 'node:20'
                            args '''
                                --network jenkins_dind
                                -v /certs/client:/certs/client:ro
                                -e DOCKER_HOST=tcp://docker:2376
                                -e DOCKER_CERT_PATH=/certs/client
                                -e DOCKER_TLS_VERIFY=1
                            '''
                        }
                    }

                    steps {
                        dir('tutor-studio') {
                            sh '''
                                set -e

                                npm ci

                                mkdir -p test-results

                                npx vitest run \
                                    --reporter=junit \
                                    --outputFile=test-results/junit.xml
                            '''
                        }
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

                stage('Test — backend (.NET)') {
                    agent {
                        docker {
                            image 'mcr.microsoft.com/dotnet/sdk:10.0'
                            args '''
                                --network jenkins_dind
                                -v /certs/client:/certs/client:ro
                                -e DOCKER_HOST=tcp://docker:2376
                                -e DOCKER_CERT_PATH=/certs/client
                                -e DOCKER_TLS_VERIFY=1
                            '''
                        }
                    }

                    steps {
                        sh '''
                            set -e

                            dotnet restore bitcraft.slnx

                            dotnet add tests/Bitcraft.UnitTests package JUnitTestLogger --version 1.1.0
                            dotnet add tests/Bitcraft.ApiIntegrationTests package JUnitTestLogger --version 1.1.0
                            dotnet add tests/Bitcraft.InfrastructureIntegrationTests package JUnitTestLogger --version 1.1.0

                            mkdir -p test-results

                            dotnet test bitcraft.slnx \
                                --logger "console;verbosity=normal" \
                                --logger "junit;LogFilePath=../../test-results/{assembly}.xml"
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
            }
        }

        stage('Approve deploy') {
            when {
                branch 'deploy/orin'
            }

            agent none

            steps {
                input(
                    message: 'Deploy Bitcraft to Jetson?',
                    ok: 'Deploy'
                )
            }
        }

        stage('Deploy Bitcraft to Jetson') {
            when {
                branch 'deploy/orin'
            }

            agent {
                label 'linux-arm64 && bitcraft-jetson'
            }

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

                        grep -qxF "$JETSON_HOST_KEY" ~/.ssh/known_hosts 2>/dev/null \
                            || printf "%s\\n" "$JETSON_HOST_KEY" >> ~/.ssh/known_hosts

                        chmod 600 ~/.ssh/known_hosts

                        export DEPLOY_SHA="$GIT_COMMIT"

                        ssh \
                            -i "$JETSON_SSH_KEY" \
                            -o BatchMode=yes \
                            "$JETSON_USER@$JETSON_HOST" \
                            "bash -se" <<REMOTE 2>&1 | tee deploy.log

                        set -euo pipefail

                        cd /home/cmpe6012/AgenticTutor/bitcraft-deploy

                        echo "Fetching..."
                        git fetch origin deploy/orin

                        echo "Deploying pinned commit: \$DEPLOY_SHA"

                        git reset --hard "\$DEPLOY_SHA"

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
}
