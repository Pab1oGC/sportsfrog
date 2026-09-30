import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { AuthProvider } from "src/auth/context/jwt/auth-provider";
import { useAuthContext } from "src/auth/hooks";
import { JWT_STORAGE_KEY } from "src/auth/context/jwt/constant";

vi.mock("src/auth/context/jwt/action", () => ({ renewSession: vi.fn() }));
import { renewSession } from "src/auth/context/jwt/action";

/** Un JWT válido (o vencido) con el exp que le pidas, para sessionStorage. */
function token(exp) {
  const base64UrlEncode = (obj) =>
    btoa(unescape(encodeURIComponent(JSON.stringify(obj))))
      .replace(/\+/g, "-")
      .replace(/\//g, "_")
      .replace(/=+$/, "");
  return `${base64UrlEncode({ alg: "none" })}.${base64UrlEncode({ exp })}.firma`;
}

/** Expone user/loading/authenticated del contexto como texto, para poder afirmar sobre ellos. */
function Sonda() {
  const { user, loading, authenticated } = useAuthContext();
  return (
    <div>
      <span data-testid="loading">{String(loading)}</span>
      <span data-testid="authenticated">{String(authenticated)}</span>
      <span data-testid="user">{user ? JSON.stringify(user) : "null"}</span>
    </div>
  );
}

function montar() {
  return render(
    <AuthProvider>
      <Sonda />
    </AuthProvider>,
  );
}

async function esperarAQueTermineDeCargar() {
  await waitFor(() => expect(screen.getByTestId("loading").textContent).toBe("false"));
}

beforeEach(() => {
  renewSession.mockReset();
});

describe("AuthProvider.checkSession", () => {
  it("arranca cargando (loading=true) antes de que checkSession termine", () => {
    renewSession.mockResolvedValue(null);
    montar();
    expect(screen.getByTestId("loading").textContent).toBe("true");
  });

  it("sin token en sessionStorage, intenta renovar en vez de dejar al usuario afuera de una", async () => {
    renewSession.mockResolvedValue(null);
    montar();

    await esperarAQueTermineDeCargar();
    expect(renewSession).toHaveBeenCalledOnce();
    expect(screen.getByTestId("authenticated").textContent).toBe("false");
  });

  it("sin token, si la renovación tiene éxito, autentica con lo que devuelve", async () => {
    renewSession.mockResolvedValue({ accessToken: "a-nuevo", organizations: [{ id: 1 }] });
    montar();

    await esperarAQueTermineDeCargar();
    expect(screen.getByTestId("authenticated").textContent).toBe("true");
    expect(JSON.parse(screen.getByTestId("user").textContent)).toEqual({
      organizations: [{ id: 1 }],
      accessToken: "a-nuevo",
    });
  });

  it("con un token válido en sessionStorage, autentica de inmediato SIN llamar a renewSession", async () => {
    sessionStorage.setItem(JWT_STORAGE_KEY, token(Date.now() / 1000 + 3600));
    localStorage.setItem("organizations", JSON.stringify([{ id: 7, slug: "liga-activa" }]));
    montar();

    await esperarAQueTermineDeCargar();
    expect(renewSession).not.toHaveBeenCalled();
    expect(screen.getByTestId("authenticated").textContent).toBe("true");
    expect(JSON.parse(screen.getByTestId("user").textContent)).toEqual({
      organizations: [{ id: 7, slug: "liga-activa" }],
      accessToken: expect.any(String),
    });
  });

  it("con un token vencido en sessionStorage, lo descarta e intenta renovar (no lo da por válido)", async () => {
    sessionStorage.setItem(JWT_STORAGE_KEY, token(Date.now() / 1000 - 3600));
    renewSession.mockResolvedValue(null);
    montar();

    await esperarAQueTermineDeCargar();
    expect(renewSession).toHaveBeenCalledOnce();
    expect(screen.getByTestId("authenticated").textContent).toBe("false");
  });

  it("un JSON corrupto en 'organizations' no revienta: el usuario válido queda con organizations vacío", async () => {
    sessionStorage.setItem(JWT_STORAGE_KEY, token(Date.now() / 1000 + 3600));
    localStorage.setItem("organizations", "{esto no es json");
    montar();

    await esperarAQueTermineDeCargar();
    expect(screen.getByTestId("authenticated").textContent).toBe("true");
    expect(JSON.parse(screen.getByTestId("user").textContent).organizations).toEqual([]);
  });
});
