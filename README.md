# InclusiveTech_SmartHome: código de Unity

Código fuente de la aplicación de Unity del proyecto **Inclusive Tech**, un sistema domótico accesible desarrollado como proyecto de grado en la Fundación Universitaria Konrad Lorenz.

> ⚠️ **Este repositorio contiene únicamente el código del proyecto de Unity**, no el proyecto completo. Se publica así, de forma intencional, por dos razones:
> 1. El proyecto usa **assets de terceros / de pago** (Asset Store) cuya licencia **no permite redistribuirlos**.
> 2. Incluye **recursos propios del equipo** (sonidos, imágenes y sprites hechos por nosotros) que no forman parte de la entrega de código.
>
> Por lo tanto, **este repositorio no es un proyecto de Unity ejecutable**, es el conjunto de scripts y perfiles de configuración para revisión del código. Para compilarlo haría falta el proyecto completo con sus escenas y assets.

---

## Qué incluye

- **`Assets/_Scripts/`**: todos los scripts de C# de la aplicación (lógica del asistente, HUD, arranque, integración con los servicios, etc.).
- **`Assets/Editor/`**: scripts con funcionalidades para el Editor de Unity (herramientas de desarrollo, botones personalizados en el Inspector, etc.).
- **`Assets/Resources/`**: perfiles (ScriptableObjects, archivos `.asset`) con la configuración del sistema.

## Qué NO incluye (y por qué)

- Assets de pago del Asset Store (prohibido redistribuirlos por licencia).
- Sonidos, imágenes, sprites y demás recursos creados por el equipo.
- Escenas, prefabs, materiales y otros assets del proyecto.
- Carpetas generadas por Unity (`Library/`, `Temp/`, `Obj/`, `Build/`, etc.).
- Cualquier clave o secreto (claves de administrador, `.env`, etc.).

---

## Estructura

```
Assets/
├── _Scripts/     # Scripts de C# de la aplicación
├── Editor/       # Scripts de herramientas del Editor
└── Resources/    # Perfiles de ScriptableObjects (.asset)
```

---

## Sistema completo

La aplicación de Unity se integra con tres microservicios que viven en sus propios repositorios:

- **it_ai_assistant**: asistente de voz Mía (STT → LLM → TTS).
- **it_wake_word**: detección de la palabra de activación "Mía".
- **it_nav_system**: navegación, grafo de la casa y control de dispositivos (MQTT).

El reconocimiento facial y la administración de usuarios viven dentro de la propia aplicación de Unity (OpenCVSharp + SQLite).

---

## Contexto académico

Proyecto de grado **Panitas: Inclusive Tech** - Fundación Universitaria Konrad Lorenz.