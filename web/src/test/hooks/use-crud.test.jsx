import { describe, expect, it, vi, beforeEach } from "vitest";
import { renderHook, act, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ConfirmProvider } from "src/components/confirm-dialog";

// useCrudDialog delega toda la obtención/escritura de datos en
// src/hooks/use-api.js (ya probado en use-api.test.jsx): mockearlo entero
// acá aísla la máquina de estados del diálogo -- abrir/cerrar, form, guardar,
// borrar -- de si el fetch/POST subyacente funciona, que no es lo que esta
// suite quiere ejercitar.
vi.mock("src/hooks/use-api", () => ({
  useApi: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  apiDelete: vi.fn(),
}));
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { useApi, apiPost, apiPut, apiDelete } from "src/hooks/use-api";
import { toast } from "sonner";
import { useCrudDialog } from "src/hooks/use-crud";

function wrapper({ children }) {
  return <ConfirmProvider>{children}</ConfirmProvider>;
}

function useApiReturn(overrides = {}) {
  return { data: [], totalCount: null, mutate: vi.fn(), isLoading: false, error: undefined, ...overrides };
}

function montar(opts) {
  return renderHook(() => useCrudDialog(opts), { wrapper });
}

beforeEach(() => {
  vi.clearAllMocks();
  useApi.mockReturnValue(useApiReturn());
});

describe("lectura de la lista", () => {
  it("expone rows = data de useApi, [] mientras no llegó nada", () => {
    useApi.mockReturnValue(useApiReturn({ data: undefined }));
    const { result } = montar({ resourceUrl: "/api/clubs", emptyForm: {}, entityName: "club" });
    expect(result.current.rows).toEqual([]);
  });

  it("rows refleja exactamente lo que devuelve useApi cuando sí hay datos", () => {
    const filas = [{ id: "1" }, { id: "2" }];
    useApi.mockReturnValue(useApiReturn({ data: filas }));
    const { result } = montar({ resourceUrl: "/api/clubs", emptyForm: {}, entityName: "club" });
    expect(result.current.rows).toBe(filas);
  });

  it("sin pageParams, pide resourceUrl tal cual (sin paginar) y rowCount es null", () => {
    montar({ resourceUrl: "/api/clubs", emptyForm: {}, entityName: "club" });
    expect(useApi).toHaveBeenCalledWith("/api/clubs");
  });

  it("rowCount es null sin pageParams, sin importar lo que traiga totalCount", () => {
    useApi.mockReturnValue(useApiReturn({ totalCount: 42 }));
    const { result } = montar({ resourceUrl: "/api/clubs", emptyForm: {}, entityName: "club" });
    expect(result.current.rowCount).toBeNull();
  });

  it("con pageParams, agrega skip/take a la url que pide useApi", () => {
    montar({ resourceUrl: "/api/athletes", emptyForm: {}, entityName: "deportista", pageParams: { skip: 0, take: 10 } });
    expect(useApi).toHaveBeenCalledWith("/api/athletes?skip=0&take=10");
  });

  it("con pageParams, rowCount es totalCount ?? 0 -- nunca null", () => {
    useApi.mockReturnValue(useApiReturn({ totalCount: 7 }));
    const { result: conDatos } = montar({
      resourceUrl: "/api/athletes",
      emptyForm: {},
      entityName: "deportista",
      pageParams: { skip: 0, take: 10 },
    });
    expect(conDatos.current.rowCount).toBe(7);

    useApi.mockReturnValue(useApiReturn({ totalCount: null }));
    const { result: sinRespuestaTodavia } = montar({
      resourceUrl: "/api/athletes",
      emptyForm: {},
      entityName: "deportista",
      pageParams: { skip: 0, take: 10 },
    });
    // totalCount null (todavía no respondió) da 0, no null -- distinto del
    // caso "sin pageParams", que si es null a propósito.
    expect(sinRespuestaTodavia.current.rowCount).toBe(0);
  });

  it("isLoading se propaga tal cual desde useApi", () => {
    useApi.mockReturnValue(useApiReturn({ isLoading: true }));
    const { result } = montar({ resourceUrl: "/api/clubs", emptyForm: {}, entityName: "club" });
    expect(result.current.isLoading).toBe(true);
  });
});

describe("openCreate", () => {
  it("emptyForm como objeto: se copia superficialmente, no se comparte la referencia", () => {
    const emptyForm = { a: 1 };
    const { result } = montar({ resourceUrl: "/x", emptyForm, entityName: "cosa" });
    act(() => result.current.openCreate());
    expect(result.current.form).toEqual({ a: 1 });
    expect(result.current.form).not.toBe(emptyForm);
  });

  it("TRAMPA REAL: la copia es superficial -- un objeto anidado en emptyForm sigue compartido", () => {
    const nested = { b: 2 };
    const emptyForm = { a: 1, nested };
    const { result } = montar({ resourceUrl: "/x", emptyForm, entityName: "cosa" });
    act(() => result.current.openCreate());

    // Mutar el objeto anidado del formulario también muta el emptyForm
    // original -- {...emptyForm} no clona en profundidad.
    result.current.form.nested.b = 999;
    expect(nested.b).toBe(999);
  });

  it("emptyForm como función: se llama para obtener el formulario (no se copia como objeto)", () => {
    const emptyForm = vi.fn(() => ({ a: 1 }));
    const { result } = montar({ resourceUrl: "/x", emptyForm, entityName: "cosa" });
    // useState(emptyForm) ya la llama una vez como inicializador perezoso al
    // montar -- React trata un argumento función de useState así, no como el
    // valor en sí. openCreate la llama una segunda vez, de forma explícita.
    act(() => result.current.openCreate());
    expect(emptyForm).toHaveBeenCalledTimes(2);
    expect(result.current.form).toEqual({ a: 1 });
  });

  it("emptyForm como función se vuelve a invocar en cada apertura, no solo al montar", () => {
    const emptyForm = vi.fn(() => ({ a: 1 }));
    const { result } = montar({ resourceUrl: "/x", emptyForm, entityName: "cosa" });
    const llamadasAlMontar = emptyForm.mock.calls.length; // 1, por el inicializador perezoso de useState
    act(() => result.current.openCreate());
    act(() => result.current.close());
    act(() => result.current.openCreate());
    expect(emptyForm).toHaveBeenCalledTimes(llamadasAlMontar + 2);
  });

  it("abre el diálogo, sin editId y sin error previo", () => {
    const { result } = montar({ resourceUrl: "/x", emptyForm: {}, entityName: "cosa" });
    act(() => result.current.openCreate());
    expect(result.current.open).toBe(true);
    expect(result.current.editId).toBeNull();
    expect(result.current.error).toBe("");
  });
});

describe("openEdit", () => {
  it("carga editId y el formulario mapeado desde la fila, y abre el diálogo", () => {
    const mapToForm = vi.fn((row) => ({ nombre: row.name }));
    const { result } = montar({ resourceUrl: "/x", emptyForm: {}, entityName: "cosa", mapToForm });
    act(() => result.current.openEdit({ id: "42", name: "ACME" }));

    expect(mapToForm).toHaveBeenCalledWith({ id: "42", name: "ACME" });
    expect(result.current.editId).toBe("42");
    expect(result.current.form).toEqual({ nombre: "ACME" });
    expect(result.current.open).toBe(true);
  });

  it("sin mapToForm, usa la fila tal cual como formulario", () => {
    const { result } = montar({ resourceUrl: "/x", emptyForm: {}, entityName: "cosa" });
    act(() => result.current.openEdit({ id: "1", name: "ACME" }));
    expect(result.current.form).toEqual({ id: "1", name: "ACME" });
  });
});

describe("close", () => {
  it("cierra el diálogo sin llamar a ninguna API", () => {
    const { result } = montar({ resourceUrl: "/x", emptyForm: {}, entityName: "cosa" });
    act(() => result.current.openCreate());
    act(() => result.current.close());
    expect(result.current.open).toBe(false);
    expect(apiPost).not.toHaveBeenCalled();
  });
});

describe("save", () => {
  it("alta: llama apiPost a resourceUrl con mapToSend(form, false)", async () => {
    apiPost.mockResolvedValue({ id: "1" });
    const mapToSend = vi.fn((form) => ({ nombre: form.nombre.toUpperCase() }));
    const { result } = montar({ resourceUrl: "/api/clubs", emptyForm: { nombre: "" }, entityName: "club", mapToSend });

    act(() => result.current.openCreate());
    act(() => result.current.setForm({ nombre: "acme" }));
    await act(() => result.current.save());

    expect(mapToSend).toHaveBeenCalledWith({ nombre: "acme" }, false);
    expect(apiPost).toHaveBeenCalledWith("/api/clubs", { nombre: "ACME" });
  });

  it("edición: llama apiPut a getUrl(editId) con mapToSend(form, true) -- contrato distinto para editar", async () => {
    apiPut.mockResolvedValue({ id: "1" });
    const mapToSend = vi.fn((form, wasEdit) => (wasEdit ? { ...form, id: undefined } : form));
    const { result } = montar({ resourceUrl: "/api/clubs", emptyForm: {}, entityName: "club", mapToSend });

    act(() => result.current.openEdit({ id: "5", nombre: "ACME" }));
    await act(() => result.current.save());

    expect(mapToSend).toHaveBeenCalledWith({ id: "5", nombre: "ACME" }, true);
    expect(apiPut).toHaveBeenCalledWith("/api/clubs/5", { nombre: "ACME", id: undefined });
  });

  it("createUrl string: se usa en vez de resourceUrl para el alta, cuando difieren", async () => {
    apiPost.mockResolvedValue({});
    const { result } = montar({
      resourceUrl: "/api/categories/9/teams",
      createUrl: "/api/categories/9/individuals",
      emptyForm: {},
      entityName: "inscripción",
    });
    act(() => result.current.openCreate());
    await act(() => result.current.save());
    expect(apiPost).toHaveBeenCalledWith("/api/categories/9/individuals", {});
  });

  it("createUrl función: se resuelve en save(), con el form más reciente (no el de cuando se armaron las opciones)", async () => {
    apiPost.mockResolvedValue({});
    const createUrl = vi.fn((form) => (form.ids.length > 1 ? "/api/bulk" : "/api/single"));
    const { result } = montar({ resourceUrl: "/api/single", createUrl, emptyForm: { ids: [] }, entityName: "cosa" });

    act(() => result.current.openCreate());
    act(() => result.current.setForm({ ids: ["1", "2", "3"] }));
    await act(() => result.current.save());

    expect(createUrl).toHaveBeenCalledWith({ ids: ["1", "2", "3"] });
    expect(apiPost).toHaveBeenCalledWith("/api/bulk", { ids: ["1", "2", "3"] });
  });

  it("buildUrl arma la dirección de edición para endpoints no RESTful", async () => {
    apiPut.mockResolvedValue({});
    const buildUrl = vi.fn((base, id) => `${base}/${id}/withdrawal`);
    const { result } = montar({ resourceUrl: "/api/roster", emptyForm: {}, entityName: "inscripción", buildUrl });

    act(() => result.current.openEdit({ id: "3" }));
    await act(() => result.current.save());

    expect(buildUrl).toHaveBeenCalledWith("/api/roster", "3");
    expect(apiPut).toHaveBeenCalledWith("/api/roster/3/withdrawal", { id: "3" });
  });

  it("al guardar con éxito: cierra el diálogo y refresca la lista (mutate)", async () => {
    const mutate = vi.fn();
    useApi.mockReturnValue(useApiReturn({ mutate }));
    apiPost.mockResolvedValue({ id: "1" });
    const { result } = montar({ resourceUrl: "/x", emptyForm: {}, entityName: "cosa" });

    act(() => result.current.openCreate());
    await act(() => result.current.save());

    expect(result.current.open).toBe(false);
    expect(mutate).toHaveBeenCalledOnce();
  });

  it("savedMessage como texto fijo: muestra ese toast al guardar", async () => {
    apiPost.mockResolvedValue({ id: "1" });
    const { result } = montar({ resourceUrl: "/x", emptyForm: {}, entityName: "cosa", savedMessage: "Club creado." });
    act(() => result.current.openCreate());
    await act(() => result.current.save());
    expect(toast.success).toHaveBeenCalledWith("Club creado.");
  });

  it("savedMessage como función recibe (result, wasEdit)", async () => {
    apiPut.mockResolvedValue({ nombre: "ACME" });
    const savedMessage = vi.fn((result, wasEdit) => `${result.nombre} ${wasEdit ? "actualizado" : "creado"}`);
    const { result } = montar({ resourceUrl: "/x", emptyForm: {}, entityName: "cosa", savedMessage });

    act(() => result.current.openEdit({ id: "1", nombre: "ACME" }));
    await act(() => result.current.save());

    expect(savedMessage).toHaveBeenCalledWith({ nombre: "ACME" }, true);
    expect(toast.success).toHaveBeenCalledWith("ACME actualizado");
  });

  it("sin savedMessage, no muestra ningún toast al guardar (el cierre del diálogo ya es la confirmación)", async () => {
    apiPost.mockResolvedValue({});
    const { result } = montar({ resourceUrl: "/x", emptyForm: {}, entityName: "cosa" });
    act(() => result.current.openCreate());
    await act(() => result.current.save());
    expect(toast.success).not.toHaveBeenCalled();
  });

  it("onSaved se llama con (result, wasEdit) tras un guardado exitoso", async () => {
    apiPost.mockResolvedValue({ id: "9" });
    const onSaved = vi.fn();
    const { result } = montar({ resourceUrl: "/x", emptyForm: {}, entityName: "cosa", onSaved });
    act(() => result.current.openCreate());
    await act(() => result.current.save());
    expect(onSaved).toHaveBeenCalledWith({ id: "9" }, false);
  });

  it("si el guardado falla, el diálogo NO se cierra y muestra el mensaje de error del servidor", async () => {
    apiPost.mockRejectedValue(new Error("El nombre ya está en uso."));
    const { result } = montar({ resourceUrl: "/x", emptyForm: {}, entityName: "cosa" });

    act(() => result.current.openCreate());
    await act(() => result.current.save());

    expect(result.current.open).toBe(true); // sigue abierto -- es el contrato
    expect(result.current.error).toBe("El nombre ya está en uso.");
    expect(result.current.saving).toBe(false);
  });

  it("un error sin mensaje propio cae en un texto genérico, no en cadena vacía", async () => {
    apiPost.mockRejectedValue(new Error());
    const { result } = montar({ resourceUrl: "/x", emptyForm: {}, entityName: "cosa" });
    act(() => result.current.openCreate());
    await act(() => result.current.save());
    expect(result.current.error).toBe("No se pudo guardar.");
  });

  it("un guardado que falla no refresca la lista ni cierra -- mutate no se llama", async () => {
    const mutate = vi.fn();
    useApi.mockReturnValue(useApiReturn({ mutate }));
    apiPost.mockRejectedValue(new Error("falló"));
    const { result } = montar({ resourceUrl: "/x", emptyForm: {}, entityName: "cosa" });
    act(() => result.current.openCreate());
    await act(() => result.current.save());
    expect(mutate).not.toHaveBeenCalled();
  });
});

describe("remove", () => {
  it("pide confirmación antes de borrar; al cancelar, no llama a apiDelete y resuelve false", async () => {
    const user = userEvent.setup();
    const { result } = montar({ resourceUrl: "/api/clubs", emptyForm: {}, entityName: "club" });

    let promesa;
    act(() => {
      promesa = result.current.remove("7");
    });
    expect(screen.getByText("Eliminar club?")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Cancelar" }));

    await expect(promesa).resolves.toBe(false);
    expect(apiDelete).not.toHaveBeenCalled();
  });

  it("al confirmar, borra, refresca la lista, avisa y resuelve true", async () => {
    const user = userEvent.setup();
    const mutate = vi.fn();
    useApi.mockReturnValue(useApiReturn({ mutate }));
    apiDelete.mockResolvedValue({});
    const { result } = montar({ resourceUrl: "/api/clubs", emptyForm: {}, entityName: "club" });

    let promesa;
    act(() => {
      promesa = result.current.remove("7");
    });
    await user.click(screen.getByRole("button", { name: "Eliminar" }));

    await expect(promesa).resolves.toBe(true);
    expect(apiDelete).toHaveBeenCalledWith("/api/clubs/7");
    expect(mutate).toHaveBeenCalledOnce();
    expect(toast.success).toHaveBeenCalledWith("club eliminado.");
  });

  it("concordancia de género: entityGender 'f' da 'eliminada', no 'eliminado'", async () => {
    const user = userEvent.setup();
    apiDelete.mockResolvedValue({});
    const { result } = montar({ resourceUrl: "/api/venues", emptyForm: {}, entityName: "sede", entityGender: "f" });

    let promesa;
    act(() => {
      promesa = result.current.remove("3");
    });
    await user.click(screen.getByRole("button", { name: "Eliminar" }));
    await promesa;

    expect(toast.success).toHaveBeenCalledWith("sede eliminada.");
  });

  it("usa buildUrl para la dirección de borrado, igual que para editar", async () => {
    const user = userEvent.setup();
    apiDelete.mockResolvedValue({});
    const buildUrl = vi.fn((base, id) => `${base}/${id}/withdrawal`);
    const { result } = montar({ resourceUrl: "/api/roster", emptyForm: {}, entityName: "inscripción", buildUrl });

    let promesa;
    act(() => {
      promesa = result.current.remove("3");
    });
    await user.click(screen.getByRole("button", { name: "Eliminar" }));
    await promesa;

    expect(buildUrl).toHaveBeenCalledWith("/api/roster", "3");
    expect(apiDelete).toHaveBeenCalledWith("/api/roster/3/withdrawal");
  });

  it("si el borrado falla en el servidor, avisa el error y resuelve false (sin refrescar la lista)", async () => {
    const user = userEvent.setup();
    const mutate = vi.fn();
    useApi.mockReturnValue(useApiReturn({ mutate }));
    apiDelete.mockRejectedValue(new Error("El club tiene equipos inscriptos."));
    const { result } = montar({ resourceUrl: "/api/clubs", emptyForm: {}, entityName: "club" });

    let promesa;
    act(() => {
      promesa = result.current.remove("7");
    });
    await user.click(screen.getByRole("button", { name: "Eliminar" }));

    await expect(promesa).resolves.toBe(false);
    expect(toast.error).toHaveBeenCalledWith("El club tiene equipos inscriptos.");
    expect(mutate).not.toHaveBeenCalled();
  });

  it("un error de borrado sin mensaje propio cae en un texto genérico", async () => {
    const user = userEvent.setup();
    apiDelete.mockRejectedValue(new Error());
    const { result } = montar({ resourceUrl: "/api/clubs", emptyForm: {}, entityName: "club" });

    let promesa;
    act(() => {
      promesa = result.current.remove("7");
    });
    await user.click(screen.getByRole("button", { name: "Eliminar" }));
    await promesa;

    expect(toast.error).toHaveBeenCalledWith("Error al eliminar.");
  });
});
