# LabQC MVP

Sistema desktop Windows para gestão de laboratório e controle de qualidade. A solução usa .NET 10, WPF, EF Core e SQLite; as regras de domínio não dependem do provedor e estão preparadas para PostgreSQL.

## Executar

1. Instale o SDK .NET 10.
2. Na pasta do projeto, execute `dotnet restore` e `dotnet run --project src/LabQC.Desktop`.
3. Primeiro acesso: usuário `admin`, senha provisória `Admin@123`. O sistema exige a criação de uma nova senha antes de abrir a tela principal.

O banco fica em `%LOCALAPPDATA%\LabQC\labqc.db`. Certificados ficam na subpasta `Certificados`. Backups manuais ficam em `Documentos\LabQC Backups`.
Para testes ou instalação portátil, defina `LABQC_DATA_DIR` para outra pasta gravável.

## Primeiro uso

1. Entre como administrador.
2. Abra **Produtos** e use **+ Parâmetro** para cadastrar Umidade, Acidez, pH etc.
3. Use **+ Produto** para cadastrar o produto.
4. Clique em **Nova versão da especificação**, escolha o produto, marque os parâmetros e configure limites e consolidação.
5. Abra **Lotes e histórico** e clique em **+ Abrir novo lote**. A configuração ativa será congelada no lote.
6. Abra **Lançar análises**, selecione o lote, crie amostras, preencha os resultados e salve. Enter desce na coluna; `Ctrl+N` cria uma amostra e `Ctrl+S` salva.
7. Quando a produção terminar, selecione o lote em **Lotes e histórico** e clique em **Fechar lote**. Dois cliques abrem o resumo completo das análises.
8. Abra **Certificados**, clique em **Emitir certificado / laudo**, informe cliente, NF e quantidade e gere o PDF A4. Somente lotes fechados ficam disponíveis para emissão.

O PDF fica em `%LOCALAPPDATA%\LabQC\Certificados` e também pode ser aberto pela lista de certificados. A emissão guarda snapshot dos resultados e SHA-256 do arquivo.

Alterar uma especificação cria outra versão; lotes antigos continuam ligados ao snapshot anterior.

Use **Minha conta** no menu lateral para alterar nome de usuário, nome completo ou senha. Para salvar mudanças posteriores, a senha atual é obrigatória.

## Validar

- `dotnet build LabQC.slnx`
- `dotnet test LabQC.slnx`

## Decisões de segurança e histórico

- Parâmetros são linhas configuráveis, não colunas fixas.
- Cada lote recebe um snapshot da versão de especificação vigente.
- Correções de resultados criam uma nova versão e invalidam a anterior; exigem perfil autorizado e justificativa.
- Liberação é uma transição de estado auditável, não edição direta de um campo.
- Certificados guardam cabeçalho e resultados congelados, PDF e SHA-256; revisões são novas versões.
- O backup usa a API online do SQLite e inclui banco, PDFs e manifesto.

## Estrutura

- `LabQC.Domain`: entidades e regras.
- `LabQC.Application`: parsing pt-BR, senhas, consolidação, conformidade e fluxo de lote.
- `LabQC.Infrastructure`: EF Core/SQLite, autenticação, resultados versionados, auditoria e backup.
- `LabQC.Reports`: certificado PDF A4 com MigraDoc/PDFsharp.
- `LabQC.Desktop`: interface WPF focada na grade de análises e teclado.
- `LabQC.Tests`: testes das regras críticas.
