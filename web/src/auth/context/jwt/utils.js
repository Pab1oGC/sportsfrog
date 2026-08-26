function b64urlDecode(str) {
  return decodeURIComponent(atob(str.replace(/-/g, "+").replace(/_/g, "/")).split("").map(c => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2)).join(""));
}

export function jwtDecode(token) {
  if (!token) return null;
  const parts = token.split(".");
  if (parts.length < 2) return null;
  return JSON.parse(b64urlDecode(parts[1]));
}

export function isValidToken(token) {
  if (!token) return false;
  try {
    const d = jwtDecode(token);
    return d && d.exp > Date.now() / 1000;
  } catch { return false; }
}

export function setSession(token) {
  if (token) {
    sessionStorage.setItem("jwt_access_token", token);
  } else {
    sessionStorage.removeItem("jwt_access_token");
  }
}
