import { useState } from 'react';
import Alert from 'react-bootstrap/Alert';
import Badge from 'react-bootstrap/Badge';
import Button from 'react-bootstrap/Button';
import Card from 'react-bootstrap/Card';
import Col from 'react-bootstrap/Col';
import Container from 'react-bootstrap/Container';
import Form from 'react-bootstrap/Form';
import Row from 'react-bootstrap/Row';
import Spinner from 'react-bootstrap/Spinner';
import { apiFetch, ApiError } from './api/client.js';
import './OllamaChat.css';

const TUTOR_ASK_PATH = '/api/tutor/ask';

const examplePrompts = [
    'Explain Docker Compose simply',
    'Tell me about RAG',
    'Create quiz questions',
];

const getErrorMessage = (status, payload) => {
    if (status === 401) {
        return 'Your session has expired. Please sign in again.';
    }

    if (status === 403) {
        return 'You do not have permission to use the AI tutor.';
    }

    if (
        payload?.code === 'Ollama.Unavailable' ||
        payload?.code === 'Ollama.RequestFailed'
    ) {
        return 'The local Ollama model is not available. Please make sure the AI services are running.';
    }

    if (payload?.code === 'Tutor.QuestionRequired') {
        return 'Please enter a prompt.';
    }

    if (payload?.code === 'Tutor.ModelInvalid') {
        return 'Please check the model name and try again.';
    }

    if (payload?.code === 'Tutor.TopKInvalid') {
        return 'The tutor memory search limit must be between 1 and 10.';
    }

    if (payload?.description) {
        return payload.description;
    }

    return 'Something went wrong while generating a response.';
};

async function askTutor({ question, model, useMemory, saveToMemory }) {
    let payload;

    try {
        payload = await apiFetch(TUTOR_ASK_PATH, {
            method: 'POST',
            json: {
                question,
                ...(model ? { model } : {}),
                useMemory,
                saveToMemory,
                topK: 5,
            },
        });
    } catch (err) {
        if (err instanceof ApiError) {
            let parsed = null;
            if (err.body) {
                try { parsed = JSON.parse(err.body); } catch { parsed = null; }
            }
            throw new Error(getErrorMessage(err.status, parsed));
        }
        throw new Error('Could not connect to the backend. Please try again.');
    }

    if (
        !payload ||
        typeof payload.answer !== 'string' ||
        typeof payload.model !== 'string' ||
        typeof payload.mode !== 'string' ||
        typeof payload.contextUsed !== 'boolean' ||
        typeof payload.memorySaved !== 'boolean' ||
        typeof payload.message !== 'string' ||
        !Array.isArray(payload.sources)
    ) {
        throw new Error('Something went wrong while generating a response.');
    }

    return payload;
}

const getModeBadge = (result) => {
    if (!result) return null;

    if (result.mode === 'general') {
        return <Badge>General chat</Badge>;
    }

    return result.contextUsed
        ? <Badge>Memory used</Badge>
        : <Badge>Answered without memory</Badge>;
};

const getSourceLabel = (source) => source.sourceType === 'conversation'
    ? 'Conversation'
    : 'Document';

function OllamaChat() {
    const [prompt, setPrompt] = useState('');
    const [model, setModel] = useState('');
    const [useMemory, setUseMemory] = useState(true);
    const [saveToMemory, setSaveToMemory] = useState(false);
    const [result, setResult] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    const handleSubmit = async (event) => {
        event.preventDefault();

        const trimmedPrompt = prompt.trim();
        const trimmedModel = model.trim();

        if (!trimmedPrompt) {
            setError('Please enter a prompt.');
            return;
        }

        if (loading) return;

        setLoading(true);
        setError('');
        setResult(null);

        try {
            const nextResult = await askTutor({
                question: trimmedPrompt,
                model: trimmedModel,
                useMemory,
                saveToMemory,
            });
            setResult(nextResult);
        } catch (requestError) {
            setError(
                requestError instanceof Error
                    ? requestError.message
                    : 'Something went wrong while generating a response.',
            );
        } finally {
            setLoading(false);
        }
    };

    const handleClear = () => {
        setPrompt('');
        setModel('');
        setResult(null);
        setError('');
    };

    return (
        <main className="ollama-page">
            <Container className="ollama-container">
                <section className="ollama-header">
                    <Badge className="ollama-badge">Local LLM</Badge>
                    <h1>AI Tutor Chat</h1>
                    <p>
                        Ask questions through Ollama, with optional tutor memory drawn from
                        uploaded documents and saved conversations.
                    </p>
                </section>

                <Row className="justify-content-center">
                    <Col xl={10}>
                        <Card className="ollama-chat-card">
                            <Card.Body>
                                <Form onSubmit={handleSubmit}>
                                    <Row className="g-3">
                                        <Col lg={8}>
                                            <Form.Group controlId="ollamaPrompt">
                                                <Form.Label>Prompt</Form.Label>
                                                <Form.Control
                                                    as="textarea"
                                                    rows={7}
                                                    value={prompt}
                                                    onChange={(event) => {
                                                        setPrompt(event.target.value);
                                                        if (error === 'Please enter a prompt.') setError('');
                                                    }}
                                                    placeholder="Ask the tutor something..."
                                                    disabled={loading}
                                                />
                                            </Form.Group>
                                        </Col>
                                        <Col lg={4}>
                                            <Form.Group controlId="ollamaModel">
                                                <Form.Label>Model</Form.Label>
                                                <Form.Control
                                                    type="text"
                                                    value={model}
                                                    onChange={(event) => setModel(event.target.value)}
                                                    placeholder="llama3.2"
                                                    disabled={loading}
                                                />
                                                <Form.Text>
                                                    Leave blank to use the backend default model.
                                                </Form.Text>
                                            </Form.Group>

                                            <Form.Group className="memory-toggle" controlId="useTutorMemory">
                                                <Form.Check
                                                    type="switch"
                                                    label="Use Tutor Memory"
                                                    checked={useMemory}
                                                    onChange={(event) => setUseMemory(event.target.checked)}
                                                    disabled={loading}
                                                />
                                                <Form.Text>
                                                    Search uploaded documents and saved conversations before answering. If nothing relevant is found, the tutor still answers normally.
                                                </Form.Text>
                                            </Form.Group>

                                            <Form.Group className="memory-toggle" controlId="saveTutorMemory">
                                                <Form.Check
                                                    type="switch"
                                                    label="Save this conversation to memory"
                                                    checked={saveToMemory}
                                                    onChange={(event) => setSaveToMemory(event.target.checked)}
                                                    disabled={loading}
                                                />
                                                <Form.Text>
                                                    Save this question and answer so the tutor can use it for future context.
                                                </Form.Text>
                                            </Form.Group>

                                            <div className="prompt-examples">
                                                <span>Examples</span>
                                                {examplePrompts.map((examplePrompt) => (
                                                    <button
                                                        key={examplePrompt}
                                                        type="button"
                                                        onClick={() => setPrompt(examplePrompt)}
                                                        disabled={loading}
                                                    >
                                                        {examplePrompt}
                                                    </button>
                                                ))}
                                            </div>
                                        </Col>
                                    </Row>

                                    {error && (
                                        <Alert className="ollama-alert" variant="danger">
                                            {error}
                                        </Alert>
                                    )}

                                    <div className="ollama-actions">
                                        <Button
                                            className="ask-button"
                                            type="submit"
                                            disabled={loading || !prompt.trim()}
                                        >
                                            {loading ? (
                                                <>
                                                    <Spinner animation="border" size="sm" />
                                                    Generating response...
                                                </>
                                            ) : (
                                                'Ask Tutor'
                                            )}
                                        </Button>
                                        <Button
                                            className="clear-button"
                                            type="button"
                                            onClick={handleClear}
                                            disabled={loading}
                                        >
                                            Clear
                                        </Button>
                                    </div>
                                </Form>
                            </Card.Body>
                        </Card>
                    </Col>
                </Row>

                <Row className="justify-content-center">
                    <Col xl={10}>
                        <Card className="ollama-response-card">
                            <Card.Body>
                                <div className="response-heading">
                                    <h2>Response</h2>
                                    {result && (
                                        <div>
                                            <span>{result.model}</span>
                                            {getModeBadge(result)}
                                        </div>
                                    )}
                                </div>

                                {loading ? (
                                    <div className="response-empty generating">
                                        <Spinner animation="border" />
                                        <span>Generating response...</span>
                                    </div>
                                ) : result ? (
                                    <>
                                        <p className="response-copy">{result.answer}</p>
                                        <Alert className="memory-status" variant={result.memorySaved ? 'success' : 'secondary'}>
                                            {result.message}
                                        </Alert>
                                    </>
                                ) : (
                                    <div className="response-empty">
                                        Your generated explanation will appear here.
                                    </div>
                                )}

                                {result?.contextUsed && (
                                    <section className="rag-sources">
                                        <h3>Sources</h3>
                                        {result.sources.map((source) => (
                                            <details key={`${source.sourceType}-${source.sourceId}`} className="rag-source-card">
                                                <summary>
                                                    <span>
                                                        <Badge className="source-type-badge">{getSourceLabel(source)}</Badge>
                                                        {source.documentName || (source.sourceType === 'conversation'
                                                            ? 'Saved conversation memory'
                                                            : `Document ${source.documentId}`)}
                                                    </span>
                                                    <small>
                                                        {source.chunkIndex !== null && source.chunkIndex !== undefined
                                                            ? `Chunk ${source.chunkIndex} · `
                                                            : ''}
                                                        Score {source.score.toFixed(3)}
                                                    </small>
                                                </summary>
                                                <p>{source.content}</p>
                                            </details>
                                        ))}
                                    </section>
                                )}
                            </Card.Body>
                        </Card>
                    </Col>
                </Row>
            </Container>
        </main>
    );
}

export default OllamaChat;
