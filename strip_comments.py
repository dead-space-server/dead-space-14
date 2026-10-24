# -*- coding: utf-8 -*-
# Удаляет все // и /// комментарии из указанных C#-файлов, сохраняя строку 1 (лицензия).
import io, re

files = [
    "Content.Shared/DeadSpace/Surgery/SurgeryToolComponent.cs",
    "Content.Shared/DeadSpace/Surgery/BodyIncisionComponent.cs",
    "Content.Shared/DeadSpace/Surgery/SurgeryDoAfterEvent.cs",
    "Content.Shared/DeadSpace/OrganDamage/OrganDamageableComponent.cs",
    "Content.Shared/DeadSpace/OrganDamage/OrganDamageVisuals.cs",
    "Content.Shared/DeadSpace/OrganDamage/OrganDamageEvents.cs",
    "Content.Shared/DeadSpace/OrganDamage/OrganDamageContext.cs",
    "Content.Shared/DeadSpace/Microscope/MicroscopeComponent.cs",
    "Content.Shared/DeadSpace/Microscope/MicroscopeUiKey.cs",
    "Content.Shared/DeadSpace/Microscope/MicroscopeBoundUserInterfaceState.cs",
    "Content.Shared/DeadSpace/Microscope/MicroscopeActionMessage.cs",
    "Content.Server/DeadSpace/OrganDamage/OrganDamageableSystem.cs",
    "Content.Server/DeadSpace/Surgery/SurgerySystem.cs",
    "Content.Server/DeadSpace/Microscope/MicroscopeSystem.cs",
    "Content.Client/DeadSpace/Microscope/UI/OrganView.cs",
    "Content.Client/DeadSpace/Microscope/UI/MicroscopeWindow.xaml.cs",
    "Content.Client/DeadSpace/Microscope/UI/WoundInfoWindow.cs",
    "Content.Client/DeadSpace/Microscope/UI/MicroscopeBoundUserInterface.cs",
    "Content.Client/DeadSpace/OrganDamage/OrganDamageVisualizerSystem.cs",
]

for path in files:
    with io.open(path, encoding="utf-8") as f:
        lines = f.read().splitlines()

    out = []
    for i, line in enumerate(lines):
        if i == 0 and line.startswith("// Мёртвый Космос"):
            out.append(line)
            continue
        if line.lstrip().startswith("//"):
            continue
        out.append(line.rstrip())

    # схлопнуть подряд идущие пустые строки
    collapsed = []
    prev_blank = False
    for line in out:
        blank = (line.strip() == "")
        if blank and prev_blank:
            continue
        collapsed.append(line)
        prev_blank = blank

    # убрать пустые строки сразу после строки лицензии и в начале/конце файла
    while collapsed and collapsed[-1].strip() == "":
        collapsed.pop()
    if len(collapsed) > 1 and collapsed[1].strip() == "":
        collapsed.pop(1)

    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.write("\n".join(collapsed) + "\n")

    print("cleaned", path)
