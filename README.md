# GitExtensions.ZimerfeldTree

Plugin para [GitExtensions](https://gitextensions.github.io/) que exibe branches **hierarquicamente** em estrutura de árvore, mostrando branches filhas.

**Versão atual: 1.0.52**

---

## Funcionalidades

### Visualização hierárquica de branches

- Janela não-modal que permanece aberta em paralelo ao GitExtensions
- Árvore dividida em três seções fixas: **LOCAL**, **REMOTES** e **TAGS**
- **LOCAL e REMOTES** combinam **ancestralidade** (parentesco real por commits / organização GitFlow) com **agrupamento por caminho** (`/`): dentro de cada nível pai, nomes com `/` viram nós-pasta. Ex.: `feature/teste` aparece como pasta `feature` → folha `teste`, e `release/2026` como `release` → `2026`. Quando `feature/*` é filha de `develop`, fica `develop` → `feature` → `teste`
- **TAGS** também agrupa por `/` (sem ancestralidade)
- LOCAL, REMOTES e TAGS exibe `(nenhuma branch local encontrada)` quando não há branches
- A janela abre **centralizada na tela** (horizontal e vertical)

### Seletor de Working Directory e Branch

- **Linha "Working Directory:"** no topo da janela contém:
  - Label fixo `Working Directory:`
  - **ComboBox** (somente seleção) populado automaticamente com todos os repositórios listados no dashboard do GitExtensions (lido de `%APPDATA%\GitExtensions\GitExtensions\GitExtensions.settings`) e quando novo repositório é criado
  - Label `Branch: <nome>` mostrando a branch em checkout no repositório exibido
- Selecionar outro repositório no dropdown recarrega a árvore para aquele repositório
- A lista do combo é recarregada automaticamente sempre que o GitExtensions troca de repositório
- A branch atual aparece destacada com **texto em negrito** e cor de seleção do sistema (`[nome]`)
- Seções da árvore mostram contadores: `LOCAL (N)`, `REMOTES (N)`, `TAGS (N)`
- Status bar inferior mostra: `Local: N  |  Remoto: N  |  Tags: N`

### Filtro em tempo real

- Campo de pesquisa filtra branches em todas as seções simultaneamente
- Filtro preserva nós pai que possuem filhos correspondentes

### Organização automática como GitFlow

- O plugin verifica se a hierarquia real (por parentesco de commits) respeita as regras do GitFlow:
  `master`/`main` na raiz, `develop` filho de `master`, e branches `feature/*`, `release/*` e `hotfix/*` nos pais esperados
- Quando detecta que a hierarquia está **fora da condição de GitFlow**, ele **aplica automaticamente** a organização GitFlow na árvore e exibe o aviso correspondente
- Nesse estado, o botão do painel de aviso mostra **"Restaurar hierarquia real"** — clicar nele volta a exibir a ancestralidade real do git
- A escolha manual do botão é respeitada e só é reavaliada ao trocar de repositório (ou reabrir a janela)

### Atualização automática

- A árvore é recarregada automaticamente ao:
  - Trocar de branch (**checkout**)
  - Trocar de repositório na UI do GitExtensions
  - Inicializar/reabrir um repositório
- Botão **Atualizar** para recarga manual

### Menu de contexto (botão direito)

| Item | Disponível para |
|------|----------------|
| Commit (N) | Sempre — abre a janela de Commit do GitExtensions; `N` = nº de alterações pendentes |
| Checkout | Local, remota, tag |
| Nova branch daqui… | Local, tag |
| Mesclar na branch atual | Local |
| Rebase na branch atual | Local |
| Renomear… | Local |
| Excluir… | Local, remota, tag |
| GitFlow… | Branch (local/remota/tag) |
| Expandir tudo | Sempre |
| Recolher tudo | Sempre |
| Atualizar | Sempre |

O item **Commit** mostra entre parênteses a quantidade de mudanças pendentes na working tree (arquivos staged, modificados e não rastreados), recalculada toda vez que o menu é aberto. Ao clicar, abre a janela de Commit do GitExtensions já apontando para o repositório em exibição.

### Janela GitFlow — branch base no Start

No painel **Start branch** da janela GitFlow, além de tipo e nome, há a opção **based on:**:

- Por padrão o dropdown fica **desabilitado** e usa `develop` como base (comportamento padrão do `git flow ... start`)
- Ao marcar o checkbox **based on:**, o dropdown é habilitado e lista as branches locais, permitindo iniciar a nova branch a partir de outra — por exemplo, uma **feature filha de outra feature pai**
- A base escolhida é passada ao comando: `git flow feature start "<nome>" "<base>"`

### Janela GitFlow — painel "Manage existing branches" (git-flow-next)

O painel foi adaptado ao **git-flow-next**, que não possui o comando `pull` nem as flags `-S`/`-p` do finish:

- **Publish** — `git flow <tipo> publish "<nome>"`: envia a branch para o remoto
- **Track** — `git flow <tipo> track "<nome>"`: cria uma branch local que rastreia a branch remota correspondente (útil para branches iniciadas por outra pessoa)
- **Update** — `git flow <tipo> update "<nome>"`: traz as mudanças da branch **pai** (ex.: develop) para a branch
- **Finish** — `git flow <tipo> finish [-k] [--no-fetch] "<nome>"`: mescla de volta e remove a branch; o checkbox **Keep branch after finish** adiciona `-k` e o checkbox **No fetch (--no-fetch)** evita a busca remota
- **Finish de `release` — pós-finalização automática**: quando o tipo é `release` e o `finish` termina sem erro, o painel automaticamente executa em sequência (com as saídas anexadas à janela de resultado):
  1. `git push <remote> <master>` (nome lido de `gitflow.branch.master`)
  2. `git push <remote> <develop>` (nome lido de `gitflow.branch.develop`)
  3. `git checkout <develop>` — só se os dois push tiveram sucesso

  O remote usado é `origin` (ou o primeiro configurado quando `origin` não existe). Se algum passo falhar, o fluxo para naquele ponto e a mensagem de erro é exibida.
- O dropdown de branch lista as branches locais **e** as remotas do tipo (com o prefixo removido), para que o **Track** possa selecionar uma branch que só existe no remoto
- Ao abrir a janela, se a branch em **checkout** corresponder a um tipo do git flow (ex.: `feature/manage`), o dropdown de tipo e o dropdown de branch já vêm pré-selecionados nesse tipo e nessa branch

#### Tratamento de erros

Quando um comando git flow falha, o resultado é exibido na janela e um aviso é mostrado. Se o erro indicar uma **branch base/produção ausente** (ex.: `couldn't find remote ref main`, `start point branch 'main' does not exist`), a mensagem orienta a verificar as branches existentes e a configuração `gitflow.branch.*`, e sugere marcar **No fetch** quando a falha for ao buscar do remoto.

### Atalhos de teclado e mouse

- **Duplo clique** em qualquer branch → checkout da branch selecionada
- **Enter** → checkout da branch selecionada
- **Botão direito** → seleciona o nó e abre o menu de contexto

### Janela não-modal persistente

- A janela permanece aberta enquanto o GitExtensions está em uso
- Fechar a janela a destrói — necessário reabrir para recarregar dados
- Singleton: uma única instância por sessão do GitExtensions

## Instalação

### Instalação Powershell/Terminal as Administrator

cd C:\GitExtensions\ZimerfeldTree\tools
.\install.ps1

### Instalação Manual

1. Copie `GitExtensions.Plugins.ZimerfeldTree.dll` para:
   ```
   C:\Program Files\GitExtensions\Plugins\
   ```
2. Reinicie o GitExtensions
3. O plugin aparece em **Plugins → ZimerfeldTree**

### Via NuGet (repositório local)

```powershell
Install-Package GitExtensions.ZimerfeldTree -Source C:\NUGET
```

---

## Hierarquia de branches — limitações

### A hierarquia é por nome, não por parentesco de commits

O plugin agrupa branches usando o separador `/` do nome — **não** pelo histórico de commits do git. `master` e `develop` são irmãos porque nenhum deles contém `/`:

```
LOCAL
├── develop      ← irmão
└── master       ← irmão
```

Para que uma branch apareça como filha de outra, o nome deve conter `/`:

```
LOCAL
└── feature/
    ├── login    ← feature/login
    └── pagamento ← feature/pagamento
```

### Branch real não pode ser nó pai de outra branch

O git armazena refs como arquivos no sistema de arquivos. Se `feature/login` já existe como branch, tentrar criar `feature/login/oauth` resulta em erro:

```
fatal: cannot lock ref 'refs/heads/feature/login/oauth':
'refs/heads/feature/login' exists; cannot create 'refs/heads/feature/login/oauth'
```

Isso ocorre porque `feature/login` seria simultaneamente um **arquivo** (a branch) e um **diretório** (pai de `oauth`), o que é impossível no sistema de arquivos.

**Solução:** use prefixos distintos ou nomes irmãos:

| Intenção | Nomes que funcionam |
|---|---|
| Sub-tarefas de login | `feature/login-oauth`, `feature/login-session` |
| Agrupador sem branch real | `feature/login/base` + `feature/login/oauth` |

### Gitflow não prevê feature filha de feature

O gitflow define uma hierarquia fixa onde todas as branches `feature/*` derivam de `develop` e são **irmãs** entre si. Sub-features são geralmente tratadas com commits separados na mesma branch ou com branches irmãs de prefixo comum.

---

## Uso

1. Abra um repositório no GitExtensions
2. Vá em **Plugins → ZimerfeldTree**
3. A janela de hierarquia fica aberta ao lado — navega, filtra, faz checkout sem sair dela

---

## Build

```powershell
# Incrementa versão, compila e gera .nupkg
# Execute como Administrador para também copiar o DLL para Plugins\
pwsh C:\NUGET\ZimerfeldTree\build.ps1
```

O script:
1. Lê a versão atual do `.nuspec` e incrementa o `build` (major.minor.**build**)
2. Atualiza `.nuspec` e `.csproj` com a nova versão
3. Compila em modo Release (`net9.0-windows`)
4. Se for Administrador, copia o DLL para `C:\Program Files\GitExtensions\Plugins\`
5. Empacota o `.nupkg` em `C:\NUGET\ZimerfeldTree\`

---

## Desinstalação

Delete o arquivo:
```
C:\Program Files\GitExtensions\Plugins\GitExtensions.Plugins.ZimerfeldTree.dll
```

O GitExtensions não é afetado pela remoção do plugin.

---

## Estrutura do projeto

```
ZimerfeldTree/
├── src/
│   └── GitExtensions.ZimerfeldTree/
│       ├── ZimerfeldTreePlugin.cs       # Ponto de entrada MEF (IGitPlugin)
│       ├── BranchHierarchyForm.cs       # Janela WinForms principal
│       ├── BranchHierarchyService.cs    # Lógica git: coleta, hierarquia, Git Flow
│       ├── BranchNode.cs                # Modelos: BranchNode, RemoteGroupNode, BranchTag
│       └── GitExtensions.ZimerfeldTree.csproj
├── build.ps1                            # Script de build e deploy
├── README.md                            # Este arquivo
└── GitExtensions.ZimerfeldTree.nuspec   # Metadados do pacote NuGet
```
