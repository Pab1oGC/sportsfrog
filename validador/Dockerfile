# Imagen del validador. Los modelos de OFIQ (~430 MB) se hornean en la imagen,
# no se descargan al arrancar.
#
# Build de producción:  docker build -t validador-fotos .
# Pruebas en contenedor: docker build --target test -t validador-fotos:test .
#                        docker run --rm validador-fotos:test

FROM python:3.11-slim AS base

RUN apt-get update && apt-get install -y --no-install-recommends \
        libgl1 libglib2.0-0 ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# python-ofiq fijado: la medición depende de la versión de OFIQ que trae.
RUN pip install --no-cache-dir \
        python-ofiq==0.3.1 \
        pillow numpy pyyaml \
        fastapi==0.115.* uvicorn python-multipart

RUN python-ofiq setup

WORKDIR /app
COPY config.yaml /app/config.yaml
COPY validador /app/validador

EXPOSE 8000
CMD ["uvicorn", "validador.api:app", "--host", "0.0.0.0", "--port", "8000"]


FROM base AS test

RUN pip install --no-cache-dir pytest httpx
COPY tests /app/tests

CMD ["python", "-m", "pytest", "-q"]


# Última etapa: es la que construye `docker build .` sin --target.
# Hereda de base, así que es la imagen de producción.
FROM base AS produccion
