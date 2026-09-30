import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, within, cleanup } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

vi.mock("src/hooks/use-api", () => ({ useApi: vi.fn() }));
import { useApi } from "src/hooks/use-api";
import { SelectionField, SelectionAthletes } from "src/components/selectors";

beforeEach(() => {
  vi.clearAllMocks();
});

describe("SelectionField", () => {
  it("muestra las opciones dadas y dispara onChange al elegir una", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <SelectionField
        label="Genero"
        value=""
        onChange={onChange}
        options={[{ value: "M", label: "Masculino" }, { value: "F", label: "Femenino" }]}
      />,
    );

    await user.click(screen.getByRole("combobox"));
    await user.click(screen.getByRole("option", { name: "Femenino" }));

    expect(onChange).toHaveBeenCalledOnce();
  });

  it("sin value elegido, muestra emptyLabel (o 'Seleccionar...' por defecto)", async () => {
    const user = userEvent.setup();
    render(<SelectionField label="Club" value="" onChange={vi.fn()} options={[]} />);
    await user.click(screen.getByRole("combobox"));
    expect(screen.getByRole("option", { name: "Seleccionar..." })).toBeInTheDocument();
  });

  it("emptyLabel personalizado reemplaza al texto por defecto", async () => {
    const user = userEvent.setup();
    render(<SelectionField label="Genero" value="" onChange={vi.fn()} options={[]} emptyLabel="Abierto" />);
    await user.click(screen.getByRole("combobox"));
    expect(screen.getByRole("option", { name: "Abierto" })).toBeInTheDocument();
  });

  it("isLoading muestra 'Cargando...' en vez del placeholder normal, y deshabilita el campo", async () => {
    const user = userEvent.setup();
    render(<SelectionField label="Club" value="" onChange={vi.fn()} options={[]} isLoading />);
    expect(screen.getByRole("combobox")).toHaveAttribute("aria-disabled", "true");
  });

  it("helperText, cuando se pasa, se muestra debajo del campo", () => {
    render(<SelectionField label="Club" value="" onChange={vi.fn()} options={[]} helperText="Elegí un club" />);
    expect(screen.getByText("Elegí un club")).toBeInTheDocument();
  });
});

describe("SelectionAthletes -- filtro por prefijo, en cualquier orden, sin tildes", () => {
  const atletas = [
    { id: "1", firstName: "Juan", lastName: "Perez" },
    { id: "2", firstName: "Pedro", lastName: "Lopez" },
    { id: "3", firstName: "José", lastName: "Ñáñez" },
  ];

  beforeEach(() => {
    useApi.mockReturnValue({ data: atletas, isLoading: false });
  });

  async function abrirYEscribir(texto) {
    cleanup(); // por si la propia prueba ya montó otra instancia antes
    const user = userEvent.setup();
    render(<SelectionAthletes value={[]} onChange={vi.fn()} />);
    const input = screen.getByRole("combobox");
    await user.click(input);
    await user.type(input, texto);
    return screen.queryByRole("listbox"); // null si ninguna opción matcheó -- MUI no dibuja el listbox
  }

  it("un prefijo sin ambigüedad (\"lop\") trae solo al deportista cuyo apellido empieza así", async () => {
    const listbox = await abrirYEscribir("lop");
    const opciones = within(listbox).getAllByRole("option");
    expect(opciones).toHaveLength(1);
    expect(opciones[0]).toHaveTextContent("Lopez Pedro");
  });

  it("busca por prefijo de PALABRA, no por substring en cualquier posición -- \"opez\" no matchea a Lopez", async () => {
    // "opez" (sin la ele inicial) no es el comienzo de ninguna palabra del
    // nombre de Lopez, así que no debería encontrarlo -- a diferencia de una
    // búsqueda por substring, que sí lo haría.
    await abrirYEscribir("opez");
    expect(screen.queryAllByRole("option")).toHaveLength(0);
  });

  it("orden de las palabras no importa: \"perez juan\" encuentra lo mismo que \"juan perez\"", async () => {
    const listboxA = await abrirYEscribir("juan perez");
    expect(within(listboxA).getAllByRole("option")).toHaveLength(1);

    const listboxB = await abrirYEscribir("perez juan");
    expect(within(listboxB).getAllByRole("option")).toHaveLength(1);
  });

  it("ignora tildes y la diéresis/virgulilla: \"jose nanez\" encuentra a \"José Ñáñez\"", async () => {
    const listbox = await abrirYEscribir("jose nanez");
    const opciones = within(listbox).getAllByRole("option");
    expect(opciones).toHaveLength(1);
    expect(opciones[0]).toHaveTextContent("Ñáñez José");
  });

  it("es insensible a mayúsculas/minúsculas", async () => {
    const listbox = await abrirYEscribir("JUAN");
    expect(within(listbox).getAllByRole("option")).toHaveLength(1);
  });

  it("un texto que no matchea ninguna palabra no trae opciones", async () => {
    await abrirYEscribir("zzz");
    expect(screen.queryAllByRole("option")).toHaveLength(0);
  });
});

describe("SelectionAthletes -- selección simple vs múltiple", () => {
  const atletas = [
    { id: "1", firstName: "Juan", lastName: "Perez" },
    { id: "2", firstName: "Pedro", lastName: "Lopez" },
  ];

  beforeEach(() => {
    useApi.mockReturnValue({ data: atletas, isLoading: false });
  });

  it("multiple=true (por defecto): elegir una opción llama onChange con un arreglo de ids", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<SelectionAthletes value={[]} onChange={onChange} />);

    const input = screen.getByRole("combobox");
    await user.click(input);
    await user.click(screen.getByRole("option", { name: /Perez Juan/ }));

    expect(onChange).toHaveBeenCalledWith(["1"]);
  });

  it("multiple=false: elegir una opción llama onChange con el id suelto, no un arreglo", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<SelectionAthletes value="" onChange={onChange} multiple={false} />);

    const input = screen.getByRole("combobox");
    await user.click(input);
    await user.click(screen.getByRole("option", { name: /Lopez Pedro/ }));

    expect(onChange).toHaveBeenCalledWith("2");
  });

  it("value ya elegido (multiple) se muestra como chip con el nombre del deportista", () => {
    render(<SelectionAthletes value={["1"]} onChange={vi.fn()} />);
    expect(screen.getByText("Perez Juan")).toBeInTheDocument();
  });

  it("un id en value que ya no existe en la lista se descarta en silencio (no rompe el render)", () => {
    expect(() => render(<SelectionAthletes value={["id-borrado"]} onChange={vi.fn()} />)).not.toThrow();
  });
});
