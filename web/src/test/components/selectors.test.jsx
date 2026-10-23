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

describe("SelectionAthletes -- acotada a un equipo (teamId)", () => {
  const nomina = [
    { athleteId: "1", firstName: "Juan", lastName: "Perez" },
    { athleteId: "2", firstName: "Pedro", lastName: "Lopez" },
  ];
  const cuerpoTecnico = [{ athleteId: "3", firstName: "Carlos", lastName: "Gomez" }];
  const todaLaOrganizacion = [
    { id: "1", firstName: "Juan", lastName: "Perez" },
    { id: "2", firstName: "Pedro", lastName: "Lopez" },
    { id: "3", firstName: "Carlos", lastName: "Gomez" },
    { id: "9", firstName: "Nadie", lastName: "DeEsteEquipo" },
  ];

  beforeEach(() => {
    // Un mock por URL en vez de un mockReturnValue unico: el componente ahora
    // llama a useApi tres veces (toda la organizacion, nomina del equipo,
    // cuerpo tecnico del equipo) y cada llamado tiene que responder lo suyo
    // -- no lo mismo que las otras dos.
    useApi.mockImplementation((url) => {
      if (url == null) return { data: undefined, isLoading: false };
      if (url.includes("/roster")) return { data: nomina, isLoading: false };
      if (url.includes("/staff")) return { data: cuerpoTecnico, isLoading: false };
      return { data: todaLaOrganizacion, isLoading: false };
    });
  });

  it("sin teamId (el uso de siempre, ver NominaPage) sigue mostrando toda la organizacion", async () => {
    const user = userEvent.setup();
    render(<SelectionAthletes value={[]} onChange={vi.fn()} />);
    await user.click(screen.getByRole("combobox"));
    expect(screen.getByRole("option", { name: /DeEsteEquipo Nadie/ })).toBeInTheDocument();
  });

  it('teamId="" (equipo aun no elegido) deshabilita el campo en vez de mostrar toda la organizacion', () => {
    render(<SelectionAthletes teamId="" value="" onChange={vi.fn()} multiple={false} />);
    expect(screen.getByRole("combobox")).toBeDisabled();
  });

  it("con teamId, solo ofrece a la nomina y al cuerpo tecnico de ESE equipo -- nunca a toda la organizacion", async () => {
    const user = userEvent.setup();
    render(<SelectionAthletes teamId="equipo-1" value="" onChange={vi.fn()} multiple={false} />);
    const input = screen.getByRole("combobox");
    await user.click(input);
    const opciones = within(screen.getByRole("listbox")).getAllByRole("option").map((o) => o.textContent);

    expect(opciones).toEqual(expect.arrayContaining(["Perez Juan", "Lopez Pedro", "Gomez Carlos"]));
    expect(opciones).not.toContain("DeEsteEquipo Nadie");
    expect(opciones).toHaveLength(3);
  });
});
