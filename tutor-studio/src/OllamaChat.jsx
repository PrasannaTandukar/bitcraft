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
import { useSelector } from 'react-redux';
import './OllamaChat.css';

const OLLAMA_GENERATE_URL = 'http://localhost:8000/api/ollama/generate';

const examplePrompts = [
    'Explain Docker Compose simply',
    'Summarize this topic',
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

    if (payload?.code === 'Ollama.PromptRequired') {
        return 'Please enter a prompt.';
    }

    if (payload?.code === 'Ollama.ModelInvalid') {
        return 'Please check the model name and try again.';
    }

    if (payload?.description) {
        return payload.description;
    }

    return 'Something went wrong while generating a response.';
};

async function generateOllamaResponse({ prompt, model, token }) {
    let response;

    try {
        response = await fetch(OLLAMA_GENERATE_URL, {
            method: 'POST',
            headers: {
                Authorization: `Bearer ${token}`,
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                prompt,
                ...(model ? { model } : {}),
            }),
        });
    } catch {
        throw new Error('Could not connect to the backend. Please try again.');
    }

    const payload = await response.json().catch(() => null);

    if (!response.ok) {
        throw new Error(getErrorMessage(response.status, payload));
    }

    if (
        !payload ||
        typeof payload.model !== 'string' ||
        typeof payload.response !== 'string'
    ) {
        throw new Error('Something went wrong while generating a response.');
    }

    return payload;
}

function OllamaChat() {
    const token = useSelector((state) => state.auth.token);
    const [prompt, setPrompt] = useState('');
    const [model, setModel] = useState('');
    const [response, setResponse] = useState('');
    const [responseModel, setResponseModel] = useState('');
    const [done, setDone] = useState(false);
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
        setResponse('');
        setResponseModel('');
        setDone(false);

        try {
            const result = await generateOllamaResponse({
                prompt: trimmedPrompt,
                model: trimmedModel,
                token,
            });

            setResponse(result.response);
            setResponseModel(result.model);
            setDone(result.done);
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
        setResponse('');
        setResponseModel('');
        setDone(false);
        setError('');
    };

    return (
        <main className="ollama-page">
            <Container className="ollama-container">
                <section className="ollama-header">
                    <Badge className="ollama-badge">Local LLM</Badge>
                    <h1>AI Tutor Chat</h1>
                    <p>
                        Ask questions and generate explanations using your local Ollama model
                        through the .NET backend.
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
                                    {response && (
                                        <div>
                                            <span>{responseModel}</span>
                                            {done && <Badge>Done</Badge>}
                                        </div>
                                    )}
                                </div>

                                {loading ? (
                                    <div className="response-empty generating">
                                        <Spinner animation="border" />
                                        <span>Generating response...</span>
                                    </div>
                                ) : response ? (
                                    <p className="response-copy">{response}</p>
                                ) : (
                                    <div className="response-empty">
                                        Your generated explanation will appear here.
                                    </div>
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
