# Contexto para IA

## Comprobado

- El módulo escolar está disponible en `/dashboard/school` y no duplica el
  modelo de datos: alumno equivale a deportista, y curso a categoría e
  inscripción.
- La navegación de sus seis flujos está cubierta por una prueba unitaria y
  el frontend compila correctamente.

## Pendiente

- Validar visualmente el módulo con una organización real y datos de ejemplo.

## Próximo paso

- Si los colegios necesitan atributos que no tiene un deportista (por ejemplo,
  turno o paralelo), acordar el contrato y la migración antes de ampliar el
  modelo de datos.
