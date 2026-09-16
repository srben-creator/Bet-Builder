# 1. Cria pastas
New-Item -ItemType Directory -Force -Path ".agents/skills" | Out-Null

# 2. Cria o aas-stack.json
@"
{
  "name": "csharp-vue-web-stack",
  "version": "1.0.0",
  "description": "Stack de desenvolvimento web enterprise: ASP.NET Core 8/9 + Vue 3 TypeScript",
  "skills": [
    "dotnet-backend", "dotnet-backend-patterns", "dotnet-architect", "csharp-pro",
    "cqrs-implementation", "markstream-vue", "vitest-skill", "tailwind-patterns",
    "frontend-design", "frontend-mobile-security-xss-scan", "api-patterns",
    "auth-implementation-patterns", "database-design", "playwright-skill", "brainstorming"
  ]
}
"@ | Out-File -FilePath "aas-stack.json" -Encoding utf8

# 3. Cria o .agents/skills.json
@"
{
  "entries": [
    {
      "path": ".agents/skills"
    }
  ]
}
"@ | Out-File -FilePath ".agents/skills.json" -Encoding utf8

# 4. Baixa todas as 14 skills
$skills = (Get-Content "aas-stack.json" | ConvertFrom-Json).skills
$repoRaw = "https://raw.githubusercontent.com/sickn33/agentic-awesome-skills/main/skills"

foreach ($s in $skills) {
    $dir = ".agents/skills/$s"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Write-Host "Baixando $s..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri "$repoRaw/$s/SKILL.md" -OutFile "$dir/SKILL.md"
}

Write-Host "`nStack C# + Vue composta e pronta para o Antigravity!" -ForegroundColor Green