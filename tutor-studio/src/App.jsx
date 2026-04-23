import React from 'react';
import BaseManager from './BaseManager.jsx'
import Container from 'react-bootstrap/Container';
import Nav from 'react-bootstrap/Nav';
import Navbar from 'react-bootstrap/Navbar';
import NavDropdown from 'react-bootstrap/NavDropdown';
import 'bootstrap/dist/css/bootstrap.min.css';
import Settings from './Settings.jsx';
import Benchmarking from './Benchmarking.jsx';
import LoginForm from './Login.jsx';
import { useState } from 'react'

function App() {

    const [page, setPage] = useState("home");
    return (
        <div style={{ height: "100%", flexDirection: "column", display: "flex" }}>
      <Navbar expand="lg" className="bg-body-tertiary" style={{ height: "10vh" }}>
            <Container>
                <Navbar.Brand onClick={() => {
                                setPage("home");
                        }} >
                    Agentic Tutor
                </Navbar.Brand>
                <Navbar.Toggle aria-controls="basic-navbar-nav" />
                <Navbar.Collapse id="basic-navbar-nav">
                    <Nav className="me-auto">
                        <Nav.Link onClick={() => {
                                    setPage("settings");
                            }} >Settings</Nav.Link>
                        <Nav.Link onClick={() => {
                                    setPage("benchmarking");
                            }} >Benchmarking</Nav.Link>
                        <Nav.Link onClick={() => {
                                    setPage("login");
                            }} >Login</Nav.Link>
                    </Nav>
                </Navbar.Collapse>
            </Container>
        </Navbar>
        {page === "home" && <BaseManager />}
        {page === "settings" && <Settings />}
        {page === "benchmarking" && <Benchmarking />}
        {page === "login" && <LoginForm />}
        </div>
  );
}

export default App;