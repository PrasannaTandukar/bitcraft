const rolePriority = ["Admin", "Editor", "Auditor"];

export function getAccountDisplay(email, roles = []) {
  const safeEmail = typeof email === "string" ? email.trim() : "";
  const localPart = safeEmail.split("@")[0] || "Account";
  const nameParts = localPart.split(/[._-]+/).filter(Boolean);
  const displayName = nameParts.length
    ? nameParts.map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join(" ")
    : "Account";
  const initials = nameParts.length
    ? nameParts.slice(0, 2).map((part) => part.charAt(0).toUpperCase()).join("")
    : "A";
  const role = rolePriority.find((value) => roles.includes(value)) || "Signed in";

  return { displayName, initials, role };
}
