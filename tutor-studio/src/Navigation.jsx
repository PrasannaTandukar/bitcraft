import { useSelector } from "react-redux";
import { NavLink, useNavigate } from "react-router-dom";
import Container from "react-bootstrap/Container";
import Nav from "react-bootstrap/Nav";
import Navbar from "react-bootstrap/Navbar";
import Dropdown from "react-bootstrap/Dropdown";
import { logout } from "./store/authSlice";
import { useDispatch } from "react-redux";
import { getNavigationGroups, isRouteVisible, routes } from "./routes.js";

function NavDropdownGroup({ label, items, roles }) {
    const visibleItems = items.filter((item) => isRouteVisible(item, roles));
    if (visibleItems.length === 0) return null;

    return (
        <Dropdown as={Nav.Item}>
            <Dropdown.Toggle as={Nav.Link} id={`nav-dropdown-${label.toLowerCase().replace(/\s+/g, "-")}`}>
                {label}
            </Dropdown.Toggle>
            <Dropdown.Menu>
                {visibleItems.map((item) => (
                    <Dropdown.Item as={NavLink} key={item.path} to={item.path} end>
                        {item.label}
                    </Dropdown.Item>
                ))}
            </Dropdown.Menu>
        </Dropdown>
    );
}

export default function Navigation() {
    const dispatch = useDispatch();
    const navigate = useNavigate();
    const { token, user, roles } = useSelector((state) => state.auth);
    const { standalone, groups } = getNavigationGroups(routes);

    const visibleStandalone = token
        ? standalone.filter((route) => isRouteVisible(route, roles))
        : standalone.filter((route) => route.guard === undefined);

    const visibleGroups = token
        ? groups.filter((group) =>
            group.items.some((item) => isRouteVisible(item, roles))
        )
        : [];

    return (
        <Navbar expand="lg" className="app-navbar">
            <Container className="app-navbar-container">
                <Navbar.Brand className="app-brand" onClick={() => navigate("/")}>
                    Agentic Tutor
                </Navbar.Brand>
                <Navbar.Toggle aria-controls="app-navbar-nav" />
                <Navbar.Collapse id="app-navbar-nav">
                    <Nav className="app-nav-links me-auto">
                        {visibleStandalone.map((route) => (
                            <Nav.Link as={NavLink} key={route.path} to={route.path} end>
                                {route.label}
                            </Nav.Link>
                        ))}
                        {visibleGroups.map((group) => (
                            <NavDropdownGroup
                                key={group.label}
                                label={group.label}
                                items={group.items}
                                roles={roles}
                            />
                        ))}
                        {!token && (
                            <Nav.Link as={NavLink} to="/login">Login</Nav.Link>
                        )}
                    </Nav>
                    {token && (
                        <div className="app-account">
                            <Dropdown align="end">
                                <Dropdown.Toggle
                                    as={Nav.Link}
                                    id="nav-user-dropdown"
                                    className="app-user-dropdown-toggle"
                                >
                                    {user ? (
                                        <span className="app-user-email">{user}</span>
                                    ) : (
                                        "Account"
                                    )}
                                </Dropdown.Toggle>
                                <Dropdown.Menu align="end">
                                    <Dropdown.Item as={NavLink} to="/profile" end>
                                        Profile
                                    </Dropdown.Item>
                                    <Dropdown.Item as={NavLink} to="/settings" end>
                                        Settings
                                    </Dropdown.Item>
                                    <Dropdown.Divider />
                                    <Dropdown.Item
                                        onClick={() => dispatch(logout())}
                                        eventKey="logout"
                                    >
                                        Logout
                                    </Dropdown.Item>
                                </Dropdown.Menu>
                            </Dropdown>
                        </div>
                    )}
                </Navbar.Collapse>
            </Container>
        </Navbar>
    );
}
