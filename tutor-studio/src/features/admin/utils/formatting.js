export function initial(value) {
  return value?.trim()?.[0]?.toUpperCase() || "U";
}
