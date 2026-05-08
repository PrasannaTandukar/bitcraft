import { useState, useEffect } from "react";
import { useSelector } from "react-redux";
import { Filemanager, getMenuOptions } from "@svar-ui/react-filemanager";
import "@svar-ui/react-filemanager/all.css";
import { Willow } from "@svar-ui/react-filemanager";
import '@svar-ui/react-core/style.css';
import '@svar-ui/react-menu/style.css';
import '@svar-ui/react-grid/style.css';
import '@svar-ui/react-uploader/style.css';

const menuOptions = (mode) => {
  switch (mode) {
    case "add":
      return getMenuOptions(mode).filter((o) => o.id === "upload");
    case "file":
    case "folder":
    case "body":
    case "multiselect":
      return false;
    default:
      return getMenuOptions(mode);
  }
};

function BaseManager() {
  const [data, setData] = useState([]);
  const token = useSelector((state) => state.auth.token);

  useEffect(() => {
    fetch("http://localhost:8000/api/documents", {
      headers: {
        "Authorization": `Bearer ${token}`,
        "Content-Type": "application/json",
      },
    })
      .then((res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
        return res.json();
      })
      .then((documents) => {
        const mapped = documents.map((doc) => ({
          id: doc.id,
          value: doc.name,
          size: null,
          date: doc.lastModifiedUtc ? new Date(doc.lastModifiedUtc) : new Date(doc.createdUtc),
          type: doc.mimeType,
        }));
        setData(mapped);
      })
      .catch((err) => console.error("Failed to fetch documents:", err));
  }, []);

  return (
    <div className="fm-container">
      <Willow>
        <Filemanager data={data} menuOptions={menuOptions} />
      </Willow>
    </div>
  );
}

export default BaseManager;