export const CONFIG = {
  appName: "SportFrog",
  serverUrl: import.meta.env.VITE_API_URL ?? "",
  auth: { method: "jwt", skip: false, redirectPath: "/dashboard" },
};
