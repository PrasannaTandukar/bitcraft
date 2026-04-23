import Button from 'react-bootstrap/Button';
import Form from 'react-bootstrap/Form';
import 'bootstrap/dist/css/bootstrap.min.css';
//TODO make this page better looking and connect to backend
function LoginForm() {
  return (
      <Form>
          <div style={{height:300,width:300,padding:15}}>
      <Form.Group className="mb-3" controlId="formBasicEmail">
        <Form.Label>Email address</Form.Label>
        <Form.Control type="email" placeholder="Enter email" />
      </Form.Group>

      <Form.Group className="mb-3" controlId="formBasicPassword">
        <Form.Label>Password</Form.Label>
        <Form.Control type="password" placeholder="Password" />
      </Form.Group>
      <Form.Group className="mb-3" controlId="formBasicCheckbox">
        <Form.Check type="checkbox" label="Keep me logged in" />
      </Form.Group>
      <Button variant="primary" type="submit">
        Submit
              </Button>
              </div>
    </Form>
  );
}

export default LoginForm;