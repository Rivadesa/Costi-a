"""Parches de texto seguros para sesiones de agente en Windows (los heredocs largos de Bash fallan).

Uso desde un script escrito con la herramienta de escritura:

    import sys; sys.path.insert(0, 'tools/agent')
    from patch import patch
    patch('dotnet/src/Costina.Server/Program.cs', [("texto viejo\n", "texto nuevo\n")])
    patch('dotnet/AGENTS.md', [], append="Parrafo nuevo al final.")

- Cada fragmento viejo debe aparecer EXACTAMENTE una vez (si no, falla antes de escribir nada).
- Respeta los finales de linea del fichero (CRLF o LF) y escribe UTF-8 sin BOM.
- Rutas relativas a la raiz del repositorio (directorio de trabajo).
"""
import io


def patch(path, pairs, append=None):
    text = io.open(path, encoding='utf-8', newline='').read()
    newline = '\r\n' if '\r\n' in text else '\n'
    for old, new in pairs:
        old, new = old.replace('\n', newline), new.replace('\n', newline)
        if text.count(old) != 1:
            raise AssertionError(f'{path}: se esperaba 1 coincidencia y hay {text.count(old)} de: {old[:70]!r}')
        text = text.replace(old, new)
    if append:
        if not text.endswith(newline):
            text += newline
        text += newline + append.replace('\n', newline) + newline
    io.open(path, 'w', encoding='utf-8', newline='').write(text)
