# Módulo escolar

## Propósito

Permitir que una organización que opera competencias entre colegios use el
panel sin crear un padrón paralelo al deportivo. Un **alumno** es un
deportista existente; un **curso** se representa por la categoría y la
inscripción/equipo donde participa.

## Comportamiento

- La página `Gestión escolar` debe explicar la equivalencia de los conceptos
  y enlazar a los flujos ya respaldados por el servidor.
- El alta y edición de alumnos abre el padrón de deportistas.
- La asignación a curso abre categorías e inscripciones, donde se selecciona
  al alumno para la categoría correspondiente.
- La página también ofrece accesos directos a competencias, credenciales,
  fixtures y reportes, para que la gestión escolar no duplique datos.

## Criterios de aceptación

- Existe una ruta autenticada `/dashboard/school` y una entrada de navegación
  "Gestión escolar".
- La página describe explícitamente las equivalencias alumno/deportista y
  curso/categoría-inscripción.
- Los seis accesos abren las rutas existentes correctas.
