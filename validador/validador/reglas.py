"""Motor de decisión: convierte medidas de OFIQ en un veredicto.

Solo decide. No mide: recibe las medidas ya calculadas. Así se puede probar
sin el contenedor de OFIQ.

Cada regla de docs/validador-fotos/reglas.md tiene un id, la medida de OFIQ
que la implementa, y el mensaje que ve el usuario. El umbral vive en
config.yaml, no aquí.
"""

from dataclasses import dataclass
from pathlib import Path

import yaml

RUTA_CONFIG = Path(__file__).resolve().parent.parent / "config.yaml"

MENSAJE_SIN_CARA = (
    "No se pudo encontrar una cara en la foto. "
    "Subí una foto donde se vea el rostro completo."
)


@dataclass(frozen=True)
class Regla:
    id: str
    medida: str
    mensaje: str


REGLAS = (
    Regla("R1", "SingleFacePresent",
          "Hay más de una persona en la foto. Subí una foto con una sola persona."),
    Regla("R2", "EyesOpen",
          "Los ojos aparecen cerrados. Subí una foto con los ojos abiertos."),
    Regla("R3", "ExpressionNeutrality",
          "La expresión no es neutra. Subí una foto sin sonrisa."),
    Regla("R4", "MouthClosed",
          "La boca aparece abierta. Subí una foto con la boca cerrada."),
    Regla("R5a", "HeadPoseYaw",
          "La cara no está de frente. Subí una foto mirando a la cámara."),
    Regla("R5b", "HeadPosePitch",
          "La cara no está de frente. Subí una foto mirando a la cámara."),
    Regla("R5c", "HeadPoseRoll",
          "La cara no está de frente. Subí una foto mirando a la cámara."),
    Regla("R6a", "LeftwardCropOfTheFaceImage",
          "La cara está descentrada. Encuadrá el rostro en el centro."),
    Regla("R6b", "RightwardCropOfTheFaceImage",
          "La cara está descentrada. Encuadrá el rostro en el centro."),
    Regla("R6c", "MarginAboveOfTheFaceImage",
          "La cara está descentrada. Encuadrá el rostro en el centro."),
    Regla("R6d", "MarginBelowOfTheFaceImage",
          "La cara está descentrada. Encuadrá el rostro en el centro."),
    Regla("R6e", "HeadSize",
          "La cara está muy lejos o muy cerca. Encuadrá el rostro en el centro."),
    Regla("R7", "Sharpness",
          "La foto está borrosa. Subí una foto más nítida."),
    Regla("R8a", "UnderExposurePrevention",
          "La foto está muy oscura. Subí una foto con luz pareja."),
    Regla("R8b", "OverExposurePrevention",
          "La foto está muy clara. Subí una foto con luz pareja."),
    Regla("R9", "IlluminationUniformity",
          "La luz cae desigual sobre la cara. Subí una foto con luz pareja."),
    Regla("R10", "BackgroundUniformity",
          "El fondo es irregular. Subí una foto con un fondo liso."),
    Regla("R11", "NaturalColour",
          "Los colores de la foto están alterados. Subí una foto con colores naturales."),
    Regla("R12", "EyesVisible",
          "Se ven lentes de sol. Subí una foto sin lentes de sol."),
    Regla("R13", "NoHeadCoverings",
          "La cabeza aparece cubierta. Subí una foto sin gorra."),
    Regla("R14", "MouthOcclusionPrevention",
          "Se ve una mascarilla. Subí una foto sin mascarilla."),
)


# Nombre corto para la advertencia "No verificado: ...".
NOMBRES = {
    "R1": "cantidad de caras",
    "R2": "ojos abiertos",
    "R3": "expresión neutra",
    "R4": "boca cerrada",
    "R5a": "pose frontal (giro)",
    "R5b": "pose frontal (inclinación)",
    "R5c": "pose frontal (rotación)",
    "R6a": "centrado (izquierda)",
    "R6b": "centrado (derecha)",
    "R6c": "centrado (arriba)",
    "R6d": "centrado (abajo)",
    "R6e": "tamaño de la cara",
    "R7": "nitidez",
    "R8a": "exposición (oscuro)",
    "R8b": "exposición (claro)",
    "R9": "iluminación uniforme",
    "R10": "fondo uniforme",
    "R11": "color natural",
    "R12": "lentes de sol",
    "R13": "cabeza cubierta",
    "R14": "mascarilla",
}


def cargar_config(ruta: Path = RUTA_CONFIG) -> dict:
    with ruta.open(encoding="utf-8") as f:
        return yaml.safe_load(f)


def evaluar(medidas: dict | None, cara_detectada: bool = True,
            config: dict | None = None) -> dict:
    """Veredicto para una foto.

    Tres resultados distintos por regla, que antes se mezclaban:

      - motivos: una regla CRÍTICA falló. Bloquea: la foto no se guarda.
      - advertencias: una regla no crítica falló. No bloquea: la foto se puede
        guardar bajo la responsabilidad de quien la sube.
      - no_verificado: la regla no tiene umbral, o OFIQ no pudo medirla. No es
        un defecto: es algo que no se revisó.

    estado:
      - "rechazada": falló una regla crítica, o no hay cara.
      - "aprobada": ninguna crítica falló. Puede traer advertencias.
    """
    cfg = config if config is not None else cargar_config()
    version = cfg["version_reglas"]
    calibrado = cfg["calibrado"]

    if not cara_detectada or medidas is None:
        # Sin cara no hay nada que evaluar: siempre crítico.
        return {
            "estado": "rechazada",
            "motivos": [MENSAJE_SIN_CARA],
            "advertencias": [],
            "no_verificado": [],
            "controles": [{"control": "R15", "ok": False, "critica": True}],
            "version_reglas": version,
            "calibrado": calibrado,
        }

    umbrales = cfg["umbrales"]
    criticas = set(cfg.get("criticas") or [])
    controles, motivos, advertencias, no_verificado = [], [], [], []

    for regla in REGLAS:
        umbral = umbrales.get(regla.id)
        valor = medidas.get(regla.medida)
        critica = regla.id in criticas

        if umbral is None or valor is None:
            # Sin corte, o OFIQ no pudo medirla. No es un defecto: queda
            # anotado que no se revisó.
            controles.append({"control": regla.id, "ok": None, "critica": critica,
                              "valor": valor, "umbral": umbral})
            _agregar(no_verificado, f"No verificado: {NOMBRES[regla.id]}.")
            continue

        ok = valor >= umbral
        controles.append({"control": regla.id, "ok": ok, "critica": critica,
                          "valor": round(valor, 2), "umbral": umbral})

        if not ok:
            _agregar(motivos if critica else advertencias, regla.mensaje)

    estado = "rechazada" if motivos else "aprobada"

    return {
        "estado": estado,
        "motivos": motivos,
        "advertencias": advertencias,
        "no_verificado": no_verificado,
        "controles": controles,
        "version_reglas": version,
        "calibrado": calibrado,
    }


def _agregar(lista: list[str], frase: str) -> None:
    """Sin repetir: varias medidas comparten un mismo mensaje (la pose, el encuadre)."""
    if frase not in lista:
        lista.append(frase)
