// Translated from bitbucket-pipelines.yml.
//
// REQUIRED JENKINS SETUP before this will run:
//
// 1. Credentials (Manage Jenkins > Credentials) — create these IDs:
//      jetson-ssh-key   : "SSH Username with private key" — username = Jetson
//                         SSH user, private key = the deploy key.
//      jetson-host      : "Secret text" — the Jetson's hostname/IP.
//      jetson-host-key  : "Secret text" — the known_hosts line for the Jetson
//                         (same value as $JETSON_HOST_KEY in the old pipeline).
//
// 2. A Jenkins agent node that is actually the Jetson (or can reach it),
//    labeled to match the `label` used in the "Deploy Bitcraft to Jetson"
//    stage below. Adjust that label to whatever you actually tag the node.
//
// 3. Docker available on whichever agent(s) run the docker-based stages
//    (scan-frontend, scan-backend, test-frontend, test-backend, and the
//    gitleaks docker-run call) — requires the Docker Pipeline plugin and a
//    reachable Docker daemon from that agent.
//
// 4. This file assumes a Multibranch Pipeline job (so `branch 'deploy/orin'`
//    resolves correctly). If this is a single-branch Pipeline job instead,
//    delete the `when { branch 'deploy/orin' }` condition — the job itself
//    already only builds that one branch.

pipeline {
    agent none

    stages {
        stage('deploy/orin pipeline') {
            stages {

                stage('Scans') {
                    parallel {

                        stage('Secret scan (gitleaks)') {
                            agent any
                            steps {
                                // Full history, not a shallow clone — gitleaks needs
                                // full git log to scan past commits, same as the
                                // Bitbucket step's `clone: depth: full`.
                                checkout([
                                    $class: 'GitSCM',
                                    branches: scm.branches,
                                    userRemoteConfigs: scm.userRemoteConfigs,
                                    extensions: [[$class: 'CloneOption', shallow: false, depth: 0, noTags: false]]
                                ])
                                // Run via `docker run` rather than using the gitleaks
                                // image as the stage's own agent — that image is a
                                // minimal scanner binary and likely has no git client,
                                // which Jenkins' automatic per-stage checkout needs.
                                sh '''
                                    docker run --rm -v "$WORKSPACE:/repo" -w /repo \
                                      zricethezav/gitleaks:latest detect \
                                      --source . --verbose \
                                      --report-format json --report-path gitleaks-report.json
                                '''
                            }
                            post {
                                always {
                                    archiveArtifacts artifacts: 'gitleaks-report.json', allowEmptyArchive: true
                                }
                            }
                        }

                        stage('Dependency scan — frontend (npm audit)') {
                            agent { docker { image 'node:20' } }
                            steps {
                                dir('tutor-studio') {
                                    sh '''
                                        npm ci
                                        npm audit --json > npm-audit-report.json || true
                                        npm audit --audit-level=high
                                    '''
                                }
                            }
                            post {
                                always {
                                    archiveArtifacts artifacts: 'tutor-studio/npm-audit-report.json', allowEmptyArchive: true
                                }
                            }
                        }

                        stage('Dependency scan — backend (dotnet)') {
                            agent { docker { image 'mcr.microsoft.com/dotnet/sdk:10.0' } }
                            steps {
                                sh '''
                                    dotnet restore bitcraft.slnx
                                    OUTPUT=$(dotnet list bitcraft.slnx package --vulnerable --include-transitive)
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
                                    archiveArtifacts artifacts: 'dotnet-vulnerable-report.txt', allowEmptyArchive: true
                                }
                            }
                        }
                    }
                }

                stage('Tests') {
                    parallel {

                        stage('Test — frontend (npm)') {
                            agent { docker { image 'node:20' } }
                            steps {
                                dir('tutor-studio') {
                                    sh '''
                                        npm ci
                                        npx vitest run --reporter=junit --outputFile=test-results/junit.xml
                                    '''
                                }
                            }
                            post {
                                always {
                                    // Jenkins' `junit` step both publishes the report in
                                    // the UI and records pass/fail trend — the Bitbucket
                                    // equivalent of type: "test-reports".
                                    junit testResults: 'tutor-studio/test-results/junit.xml', allowEmptyResults: true
                                }
                            }
                        }

                        stage('Test — backend (.NET)') {
                            agent { docker { image 'mcr.microsoft.com/dotnet/sdk:10.0' } }
                            steps {
                                sh '''
                                    dotnet restore bitcraft.slnx
                                    dotnet add tests/Bitcraft.UnitTests package JUnitTestLogger --version 1.1.0
                                    dotnet add tests/Bitcraft.ApiIntegrationTests package JUnitTestLogger --version 1.1.0
                                    dotnet add tests/Bitcraft.InfrastructureIntegrationTests package JUnitTestLogger --version 1.1.0
                                    dotnet test bitcraft.slnx --logger "console;verbosity=normal" --logger "junit;LogFilePath=../../test-results/{assembly}.xml"
                                '''
                            }
                            post {
                                always {
                                    junit testResults: 'test-results/*.xml', allowEmptyResults: true
                                }
                            }
                        }
                    }
                }

                stage('Approve deploy') {
                    // agent none here so the manual approval wait doesn't tie up
                    // an executor while someone decides whether to click "Deploy".
                    agent none
                    steps {
                        input message: 'Deploy Bitcraft to Jetson?', ok: 'Deploy'
                    }
                }

                stage('Deploy Bitcraft to Jetson') {
                    // Adjust this label to match whatever you tag your Jetson's
                    // Jenkins agent node with.
                    agent { label 'linux-arm64 && bitcraft-jetson' }
                    steps {
                        withCredentials([
                            sshUserPrivateKey(credentialsId: 'jetson-ssh-key', keyFileVariable: 'JETSON_SSH_KEY', usernameVariable: 'JETSON_USER'),
                            string(credentialsId: 'jetson-host', variable: 'JETSON_HOST'),
                            string(credentialsId: 'jetson-host-key', variable: 'JETSON_HOST_KEY')
                        ]) {
                            sh '''
                                set -o pipefail
                                mkdir -p ~/.ssh
                                chmod 700 ~/.ssh
                                grep -qxF "$JETSON_HOST_KEY" ~/.ssh/known_hosts 2>/dev/null || printf "%s\n" "$JETSON_HOST_KEY" >> ~/.ssh/known_hosts
                                chmod 600 ~/.ssh/known_hosts

                                # GIT_COMMIT is populated automatically by Jenkins' checkout step.
                                export DEPLOY_SHA="$GIT_COMMIT"

                                ssh -i "$JETSON_SSH_KEY" -o BatchMode=yes "$JETSON_USER@$JETSON_HOST" "bash -se" <<REMOTE 2>&1 | tee deploy.log
                                set -euo pipefail

                                cd /home/cmpe6012/AgenticTutor/bitcraft-deploy

                                echo "Fetching..."
                                git fetch origin deploy/orin

                                echo "Deploying pinned commit: $DEPLOY_SHA"
                                git reset --hard "$DEPLOY_SHA"
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
                            archiveArtifacts artifacts: 'deploy.log', allowEmptyArchive: true
                        }
                    }
                }
            }
        }
    }
}
