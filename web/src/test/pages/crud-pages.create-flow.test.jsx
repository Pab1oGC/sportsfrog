import { describe, expect, it, vi, beforeEach } from "vitest";
import userEvent from "@testing-library/user-event";
import { renderWithProviders as render, screen, waitFor } from "src/test/render";
import { endpoints } from "src/lib/axios";

/**
 * Suite parametrizada para el patrón que comparten varias páginas del panel:
 * PageHeader + DataGrid + CrudDialog sobre useCrudDialog. En vez de un
 * archivo casi idéntico por página, una sola tabla de configuración cubre lo
 * que de verdad es igual entre todas: la grilla muestra las filas que llegan
 * de la API, "Nuevo X" abre un diálogo con el formulario vacío, y guardar
 * manda un POST a la dirección correcta con el valor tipeado.
 *
 * Alcance deliberado: solo el alta. Editar y borrar SÍ están cubiertos a
 * fondo (cada rama de useCrudDialog, con el ConfirmProvider real) en
 * src/hooks/use-crud.test.jsx -- lo que falta ahí es la traducción de cada
 * página a botones concretos, y esa traducción no es uniforme entre páginas:
 * clubs-page usa EditDeleteActions (Tooltip envuelve directo al botón, con
 * accessible name), venues-page usa RowActionsMenu (el Tooltip envuelve un
 * <span> intermedio para poder deshabilitar el botón, y el aria-label queda
 * en ese span, no en el botón -- ver row-actions-menu.test.jsx). Forzar esa
 * diferencia real a una sola prueba genérica sería frágil sin ganar nada que
 * use-crud.test.jsx no cubra ya. Categorías/equipos/deportistas/competencias/
 * reglamentos tampoco entran acá: cada una depende de datos externos propios
 * (deporte, reglamento, competencia) que las sacan de "misma forma que las
 * demás" -- son terreno del Bloque 5 (reglas por página), no de este.
 */
vi.mock("src/hooks/use-api", () => ({ useApi: vi.fn(), apiPost: vi.fn(), apiPut: vi.fn(), apiDelete: vi.fn() }));
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
// venues-page monta un mapa Leaflet real dentro de su diálogo -- ajeno a lo
// que esta suite quiere ejercitar (el propio LocationPicker es terreno del
// Bloque 7, con un navegador real), y frágil en jsdom (mide 0 sin layout de
// verdad). Se reemplaza por un campo de texto controlable equivalente.
vi.mock("src/components/location-picker", () => ({
  LocationPicker: ({ value, onChange }) => (
    <input aria-label="Enlace de Google Maps" value={value} onChange={(e) => onChange(e.target.value)} />
  ),
}));

import { useApi, apiPost } from "src/hooks/use-api";

import ClubsPage from "src/pages/clubs-page";
import MembersPage from "src/pages/members-page";
import VenuesPage from "src/pages/venues-page";

function useApiFalso(datos) {
  const base = { totalCount: null, error: undefined, mutate: vi.fn() };
  return (url) => (url == null ? { ...base, data: undefined, isLoading: false } : { ...base, data: datos, isLoading: false });
}

const filas = [
  {
    nombre: "clubs-page (clubes)",
    Pagina: ClubsPage,
    datosLista: [{ id: "c1", name: "ACME FC", shortName: "ACME", contactEmail: "club@acme.test", isActive: true, logoUrl: null }],
    filaVisible: "ACME FC",
    nuevoLabel: "Nuevo club",
    // El título del diálogo usa el entityName propio de <CrudDialog>, que va
    // capitalizado ("Club") -- distinto del entityName en minúscula que
    // useCrudDialog usa para los mensajes de confirm()/toast ("club").
    tituloDialogo: "Nuevo Club",
    campoTexto: "Nombre",
    valorTexto: "Club Nuevo",
    endpointCrear: endpoints.clubs,
    verificarCuerpo: (body) => expect(body).toMatchObject({ name: "Club Nuevo" }),
  },
  {
    nombre: "members-page (miembros)",
    Pagina: MembersPage,
    datosLista: [{ id: "m1", email: "ana@liga.test", fullName: "Ana Gómez", role: "admin" }],
    filaVisible: "ana@liga.test",
    nuevoLabel: "Agregar miembro",
    tituloDialogo: "Nuevo Miembro",
    campoTexto: "Nombre completo",
    valorTexto: "Bruno Ríos",
    endpointCrear: endpoints.organizations.members,
    verificarCuerpo: (body) => expect(body).toMatchObject({ fullName: "Bruno Ríos" }),
  },
  {
    nombre: "venues-page (sedes)",
    Pagina: VenuesPage,
    datosLista: [{ id: "v1", name: "Coliseo Central", address: "Av. Siempre Viva", mapsUrl: "", isActive: true }],
    filaVisible: "Coliseo Central",
    nuevoLabel: "Nueva sede",
    tituloDialogo: "Nueva Sede",
    campoTexto: "Nombre",
    valorTexto: "Sede Nueva",
    endpointCrear: endpoints.venues,
    verificarCuerpo: (body) => expect(body).toMatchObject({ name: "Sede Nueva" }),
  },
];

beforeEach(() => {
  vi.clearAllMocks();
});

/**
 * MUI marca un campo `required` agregando un "*" a la etiqueta -- el nombre
 * accesible real termina siendo "Nombre *", no "Nombre" a secas (venues-page
 * lo usa; clubs/members no) -- de ahí el patrón en vez de una igualdad
 * exacta. `getByLabelText` con `exact:false` de más no sirve: el DataGrid
 * tiene su propio botón oculto "<Columna> column menu" (p. ej. "Nombre
 * column menu"), y esa cabecera casi siempre repite el mismo texto que el
 * campo del formulario -- por eso se pide el rol "textbox" además del
 * nombre, que ese botón (role="button") nunca cumple.
 */
function campoDeTexto(campoTexto) {
  return screen.getByRole("textbox", { name: new RegExp("^" + campoTexto) });
}

describe.each(filas)("$nombre", ({ Pagina, datosLista, filaVisible, nuevoLabel, tituloDialogo, campoTexto, valorTexto, endpointCrear, verificarCuerpo }) => {
  it("muestra las filas que devuelve la API", () => {
    useApi.mockImplementation(useApiFalso(datosLista));
    render(<Pagina />);
    expect(screen.getByText(filaVisible)).toBeInTheDocument();
  });

  it(`"${nuevoLabel}" abre el diálogo con el formulario vacío`, async () => {
    useApi.mockImplementation(useApiFalso(datosLista));
    const user = userEvent.setup();
    render(<Pagina />);

    await user.click(screen.getByRole("button", { name: nuevoLabel }));

    expect(screen.getByRole("heading", { name: tituloDialogo })).toBeInTheDocument();
    expect(campoDeTexto(campoTexto)).toHaveValue("");
  });

  it("completar el formulario y guardar manda un POST a la dirección correcta con el valor tipeado", async () => {
    useApi.mockImplementation(useApiFalso(datosLista));
    apiPost.mockResolvedValue({ id: "nuevo-id" });
    const user = userEvent.setup();
    render(<Pagina />);

    await user.click(screen.getByRole("button", { name: nuevoLabel }));
    await user.type(campoDeTexto(campoTexto), valorTexto);
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    expect(apiPost).toHaveBeenCalledOnce();
    const [urlLlamada, cuerpo] = apiPost.mock.calls[0];
    expect(urlLlamada).toBe(endpointCrear);
    verificarCuerpo(cuerpo);
  });

  it("guardar con éxito cierra el diálogo (el formulario ya no está en pantalla)", async () => {
    useApi.mockImplementation(useApiFalso(datosLista));
    apiPost.mockResolvedValue({ id: "nuevo-id" });
    const user = userEvent.setup();
    render(<Pagina />);

    await user.click(screen.getByRole("button", { name: nuevoLabel }));
    await user.type(campoDeTexto(campoTexto), valorTexto);
    await user.click(screen.getByRole("button", { name: "Guardar" }));

    // save() es async (espera el POST antes de cerrar) -- click() no espera
    // esa promesa, así que la aserción necesita darle margen para asentarse.
    await waitFor(() => expect(screen.queryByRole("heading", { name: tituloDialogo })).not.toBeInTheDocument());
  });
});
