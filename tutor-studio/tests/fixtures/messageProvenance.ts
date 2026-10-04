export const messageProvenance = {
  knowledge: {
    retrievedChunkCount: 5,
    similarityMetric: "cosineSimilarity",
    retrievedChunks: [
      { documentGroupKey: "document-1", documentName: "08_control_pid.md", chunkIndex: 0, retrievalRank: 1,
        excerpt: "Integral action accumulates error and removes persistent steady-state error.", similarityScore: 0.86 },
      { documentGroupKey: "document-1", documentName: "08_control_pid.md", chunkIndex: 1, retrievalRank: 2,
        excerpt: "Derivative action responds to the rate of change of the error.", similarityScore: 0.84 },
      { documentGroupKey: "document-2", documentName: "07_motors.md", chunkIndex: 2, retrievalRank: 3,
        excerpt: "A motor driver controls power supplied to the motor.", similarityScore: 0.78 },
      { documentGroupKey: "document-3", documentName: "09_sensors.md", chunkIndex: 0, retrievalRank: 4,
        excerpt: "An encoder measures motor position.", similarityScore: null },
      { documentGroupKey: "document-3", documentName: "09_sensors.md", chunkIndex: 1, retrievalRank: 5,
        excerpt: "Sensor feedback closes the control loop.", similarityScore: 0.64 },
    ],
  },
  memory: { contextProvided: true, retrievedItemCount: 2 },
  tools: [{ toolName: "Calculator", status: "Succeeded", succeeded: true, durationMs: 12, errorCategory: null }],
  runtime: { tutorId: "tutor-1", tutorRevisionId: "revision-1", revisionNumber: 1,
    snapshotId: "12345678-1234-1234-1234-123456789abc", interactionModelId: "model-1",
    providerModelName: "qwen3.8:27b", providerType: "Ollama" },
  capturedUtc: "2026-01-01T00:00:00Z",
};
