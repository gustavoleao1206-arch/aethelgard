# HeadlessTests: testes do Core sem Unity

Roda com `dotnet test`, em qualquer máquina com o .NET SDK 8 (Linux, Windows ou GitHub Actions), os testes EditMode que dependem **só** do assembly `Core`. Serve de **pré-verificação**: pega erro de compilação, C# acima da versão 9, teste que usa API que a Unity não tem (projeto `CompileCheck`) e teste quebrado, antes de abrir o projeto na Unity.

> **O Test Runner da Unity continua sendo a verdade.** Verde aqui não prova que está verde na Unity. O `UnityShim` é uma **imitação** mínima do `UnityEngine`, não a Unity de verdade.

## Como rodar

Precisa do .NET SDK 8 (no Ubuntu também serve o pacote do sistema: `apt-get install -y dotnet-sdk-8.0`).

```bash
dotnet build Tools/HeadlessTests/HeadlessTests.sln
dotnet test Tools/HeadlessTests/HeadlessTests.sln --no-build
```

O `build` explícito é obrigatório: o `dotnet test` sozinho só compila projetos de teste e **pularia o `CompileCheck`**.

Resultado esperado: `Passed: 66` e o `CompileCheck` compilando. O CI (`.github/workflows/headless-tests.yml`) roda o mesmo comando em pull request e em push na `main`, só quando mudam o Core, os testes EditMode, esta pasta ou o próprio workflow. Sem LFS, sem secrets, e um push novo cancela a execução velha do mesmo branch.

## O que cobre

**66 testes**, em 12 arquivos de `Assets/Tests/EditMode/`: `EventBusTests` (2), `Geometry2DTests` (7), `GroundSeatingTests` (5), `HouseBlueprintTests` (11), `LakeTests` (4), `PathRibbonTests` (8), `SaveMergeTests` (4), `SessionRequestTests` (2), `SimulationAuthorityTests` (3), `TimeAndRandomTests` (7), `WorldHeightTests` (8), `WorldPathsTests` (5).

Também funciona como **checagem de pureza do Core**: se alguém usar no Core algo da Unity que o shim não tem (ex.: `Physics`, `GameObject`, `Debug`), o build daqui falha.

## O que NÃO cobre

- Os outros 37 arquivos de teste: dependem de `Data`, `Gameplay`, `Net` ou de `GameObject`.
- Tudo que só a Unity tem: cenas, prefabs, `.meta`, serialização, ciclo de vida (`Awake`, `OnValidate`), física, rede (NGO) e o "null falso" dos objetos da Unity.
- `UnityEngine.Random` e `UnityEngine.Time` do shim não reproduzem a Unity: o Random usa outro gerador e o Time fica sempre em 0. Teste que precise disso injeta `GameRandom.Provider`/`GameTime.Provider`.
- **Aritmética de float idêntica à da Unity.** O `Mathf` do shim reproduz a *função* da Unity (via `System.Math`, como no UnityCsReference), mas não o arredondamento das *expressões*: o Mono do Editor calcula float em double e o .NET 8 arredonda a cada operação. Contas compostas podem diferir em ULPs.

## Como é montado

| Pasta | O que é |
|---|---|
| `UnityShim/` | `netstandard2.1`, C# 9, namespace `UnityEngine`: `Vector2`, `Vector3`, `Mathf`, `Random`, `Time`, `Object`, `Component`, `MonoBehaviour`, `SerializeField`, `DisallowMultipleComponent`. |
| `Core/` | `netstandard2.1`, C# 9. Compila os `.cs` originais de `Assets/_Project/Scripts/Core/` (nada é copiado). |
| `Tests/` | `net8.0`, C# 9, NUnit 3.14 (o NUnit 4 tirou o `Assert.AreEqual` clássico). Lista explícita dos 12 arquivos de teste originais. |
| `CompileCheck/` | **Só compila**, não roda: os mesmos 12 arquivos em `netstandard2.1` com **NUnit 3.5** — a superfície que a Unity oferece aos testes (fork `com.unity.ext.nunit`, baseado no 3.5). O `Tests/` tem API a mais (net8.0 + NUnit 3.14); se um teste usar essa API a mais, o `Tests/` fica verde e **este projeto falha**, em vez de a assembly de testes quebrar na Unity. |

**Regras do shim:** só os membros que o Core e esses testes usam, com a semântica da Unity e não a "óbvia" (ex.: em `Vector2`/`Vector3`, `==` é aproximado e `Equals` é exato; `Mathf` passa por `System.Math` em double, como a Unity). Membro faltando tem que dar **erro de compilação**, nunca comportamento silencioso. Por isso existem de propósito:
- `Mathf.Min/Max/Abs` em `int` (sem eles, uma chamada com `int` cairia em silêncio na versão `float`);
- `ToString()` de `Vector2`/`Vector3` igual ao da Unity (`F2`, cultura invariante) — é herdado de `object` e não daria erro se faltasse;
- `==`/`!=` de `UnityEngine.Object` marcados `[Obsolete(..., true)]`: comparar objeto da Unity com `null` vira **erro** aqui (o "null falso" só existe na Unity);
- **exceção deliberada:** `==`/`!=` de `Vector2`/`Vector3` existem sem uso hoje, já com a semântica aproximada da Unity, para ninguém reimplementá-los como exatos.

Se um teste falhar aqui, conserte o **shim**: nunca mude teste nem Core só para passar aqui.

## Para incluir mais um teste

1. O teste só pode depender do Core (e de NUnit). Se usar Data/Gameplay/Net, fica para um "nível 2" futuro.
2. Acrescente o mesmo `<Compile Include>` com o caminho dele em `Tests/JogoDaTrupe.Tests.Headless.csproj` **e** em `CompileCheck/JogoDaTrupe.Tests.CompileCheck.csproj` (as duas listas são iguais).
   - No teste, só API do **NUnit 3.5** e da BCL do **.NET Standard 2.1** (o `CompileCheck` cobra isso).
   - Nunca compare bits nem igualdade exata de float calculado por expressão composta: use tolerância (`Within`, `AreEqual(esperado, real, delta)`).
3. Se faltar algum membro no shim (erro de compilação), acrescente-o copiando o comportamento da Unity.
4. Atualize a contagem de testes deste README.

## Manutenção

- **.NET 8 sai de suporte em 10/11/2026.** O CI continua funcionando por um tempo; quando for trocar, mude o `TargetFramework` do `Tests/` e o `dotnet-version` do workflow juntos (o shim e o `CompileCheck` ficam em `netstandard2.1`, como a Unity).
- Este check **não pode** ser marcado como obrigatório numa proteção de branch: por causa do filtro de caminhos, um PR que não mexe no Core nunca o dispara.
