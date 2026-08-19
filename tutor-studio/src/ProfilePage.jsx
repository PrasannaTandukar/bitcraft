import { useSelector } from "react-redux";
import { Card } from "react-bootstrap";
import "./Profile.css";

function RoleBadge({ role }) {
    return (
        <span className="profile-role-badge">{role}</span>
    );
}

export default function ProfilePage() {
    const { user, roles } = useSelector((state) => state.auth);

    return (
        <main className="profile-page">
            <div className="profile-page-header">
                <h1 className="h3 mb-1">Profile</h1>
                <p className="profile-page-subtitle mb-0">
                    Your account information and roles.
                </p>
            </div>

            <Card className="profile-card">
                <Card.Body>
                    <dl className="profile-details">
                        <div>
                            <dt>Email</dt>
                            <dd>{user || "Not available"}</dd>
                        </div>
                        <div>
                            <dt>Roles</dt>
                            <dd>
                                {roles && roles.length > 0 ? (
                                    <div className="profile-roles">
                                        {roles.map((role) => (
                                            <RoleBadge key={role} role={role} />
                                        ))}
                                    </div>
                                ) : (
                                    <span className="profile-no-roles">No roles assigned</span>
                                )}
                            </dd>
                        </div>
                    </dl>
                </Card.Body>
            </Card>
        </main>
    );
}
