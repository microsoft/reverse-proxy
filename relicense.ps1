Get-ChildItem -Recurse -Filter *.cs | ForEach-Object {
    $lines = [IO.File]::ReadAllLines($_.FullName)
    if ($lines[0].StartsWith("// <copyright") -and $lines[1].StartsWith("// Copyright") -and $lines[2].StartsWith("// </copyright>")) {
        $newLines = @(
            "// Copyright (c) Microsoft Corporation.",
            "// Licensed under the MIT License."
        ) + $lines[3..$lines.Length]
        [IO.File]::WriteAllText($_.FullName, [string]::Join("`n", $newLines))
    }
}