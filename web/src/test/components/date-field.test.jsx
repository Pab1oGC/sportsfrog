import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

/**
 * DateField/DateTimeField/TimeField son una capa fina sobre los pickers de
 * @mui/x-date-pickers: la única lógica propia de este archivo es la
 * conversión de valores (string ISO/vacío <-> dayjs) y el contrato de evento
 * sintético { target: { value } }. Probar eso a través del picker real de
 * verdad (calendario, popper, teclas por sección) sería probar la librería,
 * no este archivo -- así que se reemplaza cada picker por un doble mínimo,
 * controlable con botones, que expone en el DOM exactamente lo que este
 * componente le pasó (value/format/disabled/minDate/maxDate) y permite
 * simular las tres transiciones que le importan a la conversión: elegir un
 * valor válido, limpiar (null) y elegir algo inválido.
 */
function crearRecogedorFalso(testId) {
  return function RecogedorFalso(props) {
    return (
      <div data-testid={testId}>
        <span data-testid={`${testId}-value`}>{props.value ? "presente" : "null"}</span>
        <span data-testid={`${testId}-format`}>{props.format}</span>
        <span data-testid={`${testId}-disabled`}>{String(!!props.disabled)}</span>
        <span data-testid={`${testId}-minDate`}>{props.minDate ? props.minDate.format("YYYY-MM-DD") : "undefined"}</span>
        <span data-testid={`${testId}-maxDate`}>{props.maxDate ? props.maxDate.format("YYYY-MM-DD") : "undefined"}</span>
        <span data-testid={`${testId}-error`}>{String(props.slotProps?.textField?.error)}</span>
        <button onClick={() => props.onChange({ isValid: () => true, format: () => "VALOR_ELEGIDO" })}>
          {testId}-elegir
        </button>
        <button onClick={() => props.onChange(null)}>{testId}-limpiar</button>
        <button onClick={() => props.onChange({ isValid: () => false, format: () => "no-deberia-verse" })}>
          {testId}-invalido
        </button>
      </div>
    );
  };
}

vi.mock("@mui/x-date-pickers/DatePicker", () => ({ DatePicker: crearRecogedorFalso("date") }));
vi.mock("@mui/x-date-pickers/DesktopDateTimePicker", () => ({ DesktopDateTimePicker: crearRecogedorFalso("datetime") }));
vi.mock("@mui/x-date-pickers/DesktopTimePicker", () => ({ DesktopTimePicker: crearRecogedorFalso("time") }));

import { DateField, DateTimeField, TimeField } from "src/components/date-field";

describe("DateField", () => {
  it("value vacío/null pasa value=null al picker (no una fecha inválida)", () => {
    render(<DateField label="Fecha" value="" onChange={vi.fn()} />);
    expect(screen.getByTestId("date-value")).toHaveTextContent("null");
  });

  it("un value ISO válido se parsea a dayjs antes de pasarlo al picker", () => {
    render(<DateField label="Fecha" value="2026-03-15" onChange={vi.fn()} />);
    expect(screen.getByTestId("date-value")).toHaveTextContent("presente");
  });

  it("un value que no es una fecha real pasa null al picker, no una fecha inválida", () => {
    render(<DateField label="Fecha" value="no-es-una-fecha" onChange={vi.fn()} />);
    expect(screen.getByTestId("date-value")).toHaveTextContent("null");
  });

  it("elegir una fecha llama a onChange con el evento sintético { target: { value } } en formato AAAA-MM-DD", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<DateField label="Fecha" value="" onChange={onChange} />);
    await user.click(screen.getByText("date-elegir"));
    expect(onChange).toHaveBeenCalledWith({ target: { value: "VALOR_ELEGIDO" } });
  });

  it("limpiar la fecha (picker -> null) llama a onChange con value: '', no con null", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<DateField label="Fecha" value="2026-03-15" onChange={onChange} />);
    await user.click(screen.getByText("date-limpiar"));
    expect(onChange).toHaveBeenCalledWith({ target: { value: "" } });
  });

  it("un valor inválido del picker llama a onChange con value: '' -- nunca deja pasar una fecha inválida", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<DateField label="Fecha" value="" onChange={onChange} />);
    await user.click(screen.getByText("date-invalido"));
    expect(onChange).toHaveBeenCalledWith({ target: { value: "" } });
  });

  it("fuerza el formato DD/MM/YYYY sin importar el idioma del navegador", () => {
    render(<DateField label="Fecha" value="" onChange={vi.fn()} />);
    expect(screen.getByTestId("date-format")).toHaveTextContent("DD/MM/YYYY");
  });

  it("minDate/maxDate se convierten a dayjs cuando se pasan, undefined cuando no", () => {
    const { rerender } = render(<DateField label="Fecha" value="" onChange={vi.fn()} />);
    expect(screen.getByTestId("date-minDate")).toHaveTextContent("undefined");
    expect(screen.getByTestId("date-maxDate")).toHaveTextContent("undefined");

    rerender(<DateField label="Fecha" value="" onChange={vi.fn()} minDate="2026-01-01" maxDate="2026-12-31" />);
    expect(screen.getByTestId("date-minDate")).toHaveTextContent("2026-01-01");
    expect(screen.getByTestId("date-maxDate")).toHaveTextContent("2026-12-31");
  });

  it("disabled y error se propagan al picker/al textField", () => {
    render(<DateField label="Fecha" value="" onChange={vi.fn()} disabled error />);
    expect(screen.getByTestId("date-disabled")).toHaveTextContent("true");
    expect(screen.getByTestId("date-error")).toHaveTextContent("true");
  });
});

describe("DateTimeField", () => {
  it('""/null pasa value=null al picker', () => {
    render(<DateTimeField label="Inicio" value="" onChange={vi.fn()} />);
    expect(screen.getByTestId("datetime-value")).toHaveTextContent("null");
  });

  it("un ISO local válido se parsea antes de pasarlo al picker", () => {
    render(<DateTimeField label="Inicio" value="2026-05-01T16:00" onChange={vi.fn()} />);
    expect(screen.getByTestId("datetime-value")).toHaveTextContent("presente");
  });

  it("elegir llama a onChange con formato AAAA-MM-DDTHH:mm", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<DateTimeField label="Inicio" value="" onChange={onChange} />);
    await user.click(screen.getByText("datetime-elegir"));
    expect(onChange).toHaveBeenCalledWith({ target: { value: "VALOR_ELEGIDO" } });
  });

  it("limpiar o un valor inválido llaman a onChange con value: ''", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<DateTimeField label="Inicio" value="2026-05-01T16:00" onChange={onChange} />);

    await user.click(screen.getByText("datetime-limpiar"));
    expect(onChange).toHaveBeenLastCalledWith({ target: { value: "" } });

    await user.click(screen.getByText("datetime-invalido"));
    expect(onChange).toHaveBeenLastCalledWith({ target: { value: "" } });
  });

  it("formato de 12 horas con AM/PM, igual que el resto de la app", () => {
    render(<DateTimeField label="Inicio" value="" onChange={vi.fn()} />);
    expect(screen.getByTestId("datetime-format")).toHaveTextContent("DD/MM/YYYY hh:mm A");
  });
});

describe("TimeField", () => {
  it('""/null pasa value=null al picker', () => {
    render(<TimeField label="Hora" value="" onChange={vi.fn()} />);
    expect(screen.getByTestId("time-value")).toHaveTextContent("null");
  });

  it("ancla 'HH:mm' a TIME_ONLY_REFERENCE_DATE (dayjs no tiene un tipo 'solo hora')", () => {
    render(<TimeField label="Hora" value="14:30" onChange={vi.fn()} />);
    expect(screen.getByTestId("time-value")).toHaveTextContent("presente");
  });

  it("un 'HH:mm' que no es una hora real pasa value=null, no una hora inválida", () => {
    // "25:99" no sirve como ejemplo: dayjs delega en el parseo nativo de
    // Date, que hace "roll-over" y lo acepta como una fecha válida un poco
    // más adelante -- "aa:bb" es lo que de verdad no logra parsearse.
    render(<TimeField label="Hora" value="aa:bb" onChange={vi.fn()} />);
    expect(screen.getByTestId("time-value")).toHaveTextContent("null");
  });

  it("elegir llama a onChange con formato HH:mm (sin fecha)", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<TimeField label="Hora" value="" onChange={onChange} />);
    await user.click(screen.getByText("time-elegir"));
    expect(onChange).toHaveBeenCalledWith({ target: { value: "VALOR_ELEGIDO" } });
  });

  it("limpiar o un valor inválido llaman a onChange con value: ''", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<TimeField label="Hora" value="14:30" onChange={onChange} />);

    await user.click(screen.getByText("time-limpiar"));
    expect(onChange).toHaveBeenLastCalledWith({ target: { value: "" } });

    await user.click(screen.getByText("time-invalido"));
    expect(onChange).toHaveBeenLastCalledWith({ target: { value: "" } });
  });

  it("formato hh:mm A (12 horas), igual criterio que DateTimeField", () => {
    render(<TimeField label="Hora" value="" onChange={vi.fn()} />);
    expect(screen.getByTestId("time-format")).toHaveTextContent("hh:mm A");
  });
});
