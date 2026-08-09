import Form from "react-bootstrap/Form";

export default function TutorSelector({ tutors, selectedTutorId, disabled, onChange }) {
  return (
    <Form.Group controlId="runtime-tutor-selector" className="runtime-tutor-selector">
      <Form.Label>Tutor</Form.Label>
      <Form.Select
        value={selectedTutorId}
        onChange={(event) => onChange(event.target.value)}
        disabled={disabled}
      >
        <option value="">Select an active tutor</option>
        {tutors.map((tutor) => (
          <option key={tutor.tutorId} value={tutor.tutorId}>{tutor.tutorName}</option>
        ))}
      </Form.Select>
    </Form.Group>
  );
}
