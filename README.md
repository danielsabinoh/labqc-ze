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
2. Abra **Produtos** e use **+ Parâmetro** para cadastrar Umidade, Acidez, pH etc. A busca filtra por nome, código, categoria ou unidade.
3. Use **+ Produto** para cadastrar o produto e configure suas análises quando o sistema perguntar. Um produto também pode ser alterado, duplicado ou arquivado nessa mesma tela.
4. Abra **Lançar análises**, escolha primeiro a família padronizada (**Farinha**, **Polvilho**, **Fécula**, **Amido** ou **Outros**), depois o produto/unidade e, por fim, o lote aberto. Se o lote ainda não existir, use **+ Iniciar novo lote** sem sair da tela.
5. Na abertura do lote, informe produto, número e data de fabricação. A validade é calculada automaticamente e as análises exigidas aparecem antes da confirmação.
6. Crie amostras, preencha os resultados e salve. Enter desce na coluna; `Ctrl+N` cria uma amostra e `Ctrl+S` salva.
7. Quando a produção terminar, selecione o lote em **Lotes e histórico** e clique em **Fechar lote**. Dois cliques abrem o resumo completo das análises.
8. Abra **Certificados**, clique em **Emitir certificado / laudo**, informe cliente, NF e quantidade e gere o PDF A4. Somente lotes fechados ficam disponíveis para emissão.

Na emissão do laudo, o botão **Importar XML da NF-e** preenche automaticamente destinatário, cidade, UF, número da nota e quantidade. O lote, o produto e a unidade continuam vindo do cadastro do LabQC. Quando a NF-e possui vários itens, o sistema solicita qual item fornecerá a quantidade do certificado. O preenchimento manual continua disponível.

O PDF fica em `%LOCALAPPDATA%\LabQC\Certificados` e também pode ser aberto pela lista de certificados. A emissão guarda snapshot dos resultados e SHA-256 do arquivo.

Alterar uma especificação cria outra versão; lotes antigos continuam ligados ao snapshot anterior.

O dashboard mostra lotes abertos, pendências, resultados fora da especificação e os lotes recentes. Produtos e parâmetros sem histórico são excluídos; quando já foram utilizados, são apenas arquivados para preservar resultados e laudos antigos.

A família é definida no cadastro do produto e também pode ser alterada ou preservada ao duplicá-lo. Na atualização de bancos anteriores, nomes contendo Farinha, Polvilho, Fécula ou Amido são classificados automaticamente; os demais entram em Outros.

Use **Minha conta** no menu lateral para alterar nome de usuário, nome completo ou senha. Para salvar mudanças posteriores, a senha atual é obrigatória.

Administradores também possuem a área **Usuários e acessos**. Nela é possível criar um acesso individual com perfil de Analista, Responsável da Qualidade ou Administrador, além de ativar e desativar usuários. Toda senha criada nessa tela é provisória e deve ser trocada pela própria pessoa no primeiro login.

A interface utiliza a identidade visual da Alimentos do Zé, com ícone próprio no executável, logo institucional no login e no menu, e informações da J. C. Oliveira & Filhos Ltda. na área administrativa.

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
