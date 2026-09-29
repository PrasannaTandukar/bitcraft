import { Form, Select } from "antd";

export default function TutorSelector({ tutors, selectedTutorId, disabled, onChange }) {
  return (
    <Form.Item className="runtime-tutor-selector" label="Active tutor">
      <Select
        showSearch
        optionFilterProp="label"
        value={selectedTutorId || undefined}
        onChange={onChange}
        disabled={disabled}
        placeholder="Select an active tutor"
        options={tutors.map((tutor) => ({ value: tutor.tutorId, label: tutor.tutorName }))}
      />
    </Form.Item>
  );
}
