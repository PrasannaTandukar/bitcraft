import { Image, Spin, Typography } from "antd";
import { useEffect, useState } from "react";
import { getChatImage } from "./aiTutorApi.js";
import { getTutorRuntimeError } from "./aiTutorUtils.js";

// Preview bytes are authenticated through the shared client; blob URLs never enter history.
export default function AttachmentImage({ attachment }) {
  const [url, setUrl] = useState("");
  const [error, setError] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    let objectUrl;
    getChatImage(attachment.id, controller.signal).then((blob) => {
      if (controller.signal.aborted) return;
      objectUrl = URL.createObjectURL(blob);
      setUrl(objectUrl);
    }).catch((failure) => {
      if (!controller.signal.aborted) setError(getTutorRuntimeError(failure));
    });
    return () => {
      controller.abort();
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [attachment.id]);

  if (error) return <Typography.Text type="secondary" className="runtime-image-error">{error}</Typography.Text>;
  if (!url) return <Spin size="small" aria-label="Loading image" />;
  return <Image src={url} alt={attachment.fileName || "Attached image"} width={120} height={90} className="runtime-image" />;
}
