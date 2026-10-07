"""API HTTP: recibe una foto y devuelve el veredicto.

POST /validar   multipart con el campo "archivo"  -> veredicto (JSON)
GET  /salud     estado del servicio y versiones
"""

import io
from contextlib import asynccontextmanager

import numpy as np
from fastapi import FastAPI, File, HTTPException, UploadFile
from PIL import Image, ImageOps, UnidentifiedImageError

from validador.reglas import cargar_config, evaluar

MAXIMO_BYTES = 10 * 1024 * 1024
MINIMO_ANCHO = 400
MINIMO_ALTO = 500
FORMATOS_PERMITIDOS = {"JPEG", "PNG"}

MENSAJE_FORMATO = "El archivo tiene que ser una foto en formato JPG o PNG."
MENSAJE_TAMANO = (
    f"La foto es muy chica. El mínimo es {MINIMO_ANCHO} × {MINIMO_ALTO} píxeles."
)


def decodificar(contenido: bytes) -> Image.Image:
    """La imagen, derecha y en RGB. Responde 422 si no es una foto válida."""
    try:
        img = Image.open(io.BytesIO(contenido))
        if img.format not in FORMATOS_PERMITIDOS:
            raise HTTPException(422, MENSAJE_FORMATO)
        img.load()
    except (UnidentifiedImageError, OSError):
        raise HTTPException(422, MENSAJE_FORMATO)

    # El celular guarda la rotación en metadatos. Aplicarla acá hace que OFIQ
    # mida la foto como la ve el usuario.
    img = ImageOps.exif_transpose(img).convert("RGB")
    return img


def formato_insuficiente(img: Image.Image) -> bool:
    ancho, alto = img.size
    return ancho < MINIMO_ANCHO or alto < MINIMO_ALTO


def crear_app(medidor=None, config: dict | None = None) -> FastAPI:
    """Construye la app. El medidor y la config se inyectan para poder probarla
    sin el modelo de OFIQ. En producción se crean una sola vez al arrancar."""

    @asynccontextmanager
    async def ciclo_de_vida(app: FastAPI):
        if medidor is None:
            from validador.medicion import Medidor
            app.state.medidor = Medidor()
        else:
            app.state.medidor = medidor
        app.state.config = config if config is not None else cargar_config()
        yield

    app = FastAPI(title="Validador de fotos", lifespan=ciclo_de_vida)

    @app.get("/salud")
    async def salud():
        cfg = app.state.config
        return {
            "estado": "ok",
            "version_reglas": cfg["version_reglas"],
            "calibrado": cfg["calibrado"],
        }

    @app.post("/validar")
    async def validar(archivo: UploadFile = File(...)):
        contenido = await archivo.read()
        if len(contenido) > MAXIMO_BYTES:
            raise HTTPException(413, "La foto pesa más de 10 MB.")

        img = decodificar(contenido)

        if formato_insuficiente(img):
            # Crítico: por debajo del mínimo no hay foto que evaluar.
            return {
                "estado": "rechazada",
                "motivos": [MENSAJE_TAMANO],
                "advertencias": [],
                "no_verificado": [],
                "controles": [{"control": "formato", "ok": False, "critica": True,
                               "valor": list(img.size),
                               "umbral": [MINIMO_ANCHO, MINIMO_ALTO]}],
                "version_reglas": app.state.config["version_reglas"],
                "calibrado": app.state.config["calibrado"],
            }

        medidas, cara = app.state.medidor.medir(np.asarray(img, dtype=np.uint8))
        return evaluar(medidas, cara_detectada=cara, config=app.state.config)

    return app


app = crear_app()
