-- Dados iniciais do site (serviços e números da home).
-- Gerado a partir do banco SQL Server local em 2026-11-06.
-- Aplicar depois das migrations: psql "<connection string>" -f db/seed.sql

BEGIN;

INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (1, 'Software', 'Configuração de Antivírus', 'Instalação, configuração e verificação completa contra antivírus.', 100.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (2, 'Dados', 'Configuração de Backup Automático', 'Configuração de rotinas automáticas de backup para proteção contínua dos dados.', 120.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (3, 'Software', 'Otimização de Desempenho', 'Ajustes e configurações para melhorar a velocidade e desempenho geral do computador.', 150.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (4, 'Hardware', 'Substituição de Fonte de Alimentação', 'Troca da fonte de alimentação defeituosa ou subdimensionada.', 180.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (5, 'Dados', 'Backup e Restauração de Dados', 'Criação de backup completo e restauração de dados do sistema ou arquivos importantes.', 120.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (6, 'Hardware', 'Reparo e Troca de Placas (Quando Viável)', 'Análise, reparo ou substituição de placas de vídeo, rede, som e outras placas.', 250.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (7, 'Software', 'Atualização de Drivers e BIOS', 'Atualização de drivers de hardware e da BIOS para garantir estabilidade e compatibilidade.', 100.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (8, 'Hardware', 'Troca de Pasta Térmica', 'Substituição da pasta térmica do processador para melhor dissipação de calor.', 70.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (9, 'Dados', 'Migração de Sistema para SSD', 'Migração completa do sistema operacional e dados para um novo SSD.', 200.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (10, 'Hardware', 'Troca de Teclado, Touchpad e Tela (Notebooks)', 'Substituição de teclado, touchpad e tela de notebooks danificados.', 300.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (11, 'Hardware', 'Troca de Cooler e Dissipadores', 'Substituição de cooler e dissipadores de calor defeituosos ou inadequados.', 150.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (12, 'Hardware', 'Troca e Expansão de Memória RAM', 'Substituição ou adição de módulos de memória RAM para melhorar o desempenho.', 200.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (13, 'Software', 'Softwares de Edição (Quando Permitido por Licença)', 'Instalação de softwares de edição de imagem, vídeo ou áudio conforme licenciamento.', 150.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (14, 'Hardware', 'Limpeza Interna Completa (Desktop/Notebook)', 'Desmontagem e limpeza interna completa do equipamento, removendo poeira e sujeira acumulada.', 130.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (15, 'Software', 'Redução de Aquecimento Excessivo', 'Diagnóstico e solução de problemas de superaquecimento no computador.', 120.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (16, 'Hardware', 'Troca de HD por SSD', 'Substituição do HD convencional por SSD para ganho significativo de velocidade.', 250.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (17, 'Dados', 'Backup de Dados Pessoais', 'Cópia de segurança de fotos, documentos e arquivos pessoais do cliente.', 100.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (18, 'Dados', 'Recuperação de Arquivos Apagados (Casos Simples)', 'Recuperação de arquivos deletados acidentalmente em casos de complexidade simples.', 120.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (19, 'Dados', 'Limpeza de Arquivos e Programas Desnecessários', 'Remoção de arquivos temporários, programas inúteis e sujeira do sistema.', 80.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (20, 'Dados', 'Clonagem de HD/SSD', 'Clonagem completa do disco rígido ou SSD para outro dispositivo.', 150.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (21, 'Software', 'Instalação e Atualização de Sistemas Operacionais', 'Instalação ou atualização de sistemas operacionais Windows e Linux.', 120.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (22, 'Software', 'Instalação de Programas Essenciais', 'Instalação de programas básicos e essenciais para o uso do computador.', 80.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (23, 'Software', 'Pacote Office', 'Instalação e configuração do Microsoft Office ou pacote equivalente.', 150.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (24, 'Software', 'Correção de Erros de Inicialização', 'Diagnóstico e reparo de erros que impedem o sistema de inicializar corretamente.', 150.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (25, 'Hardware', 'Montagem de Computadores Personalizados', 'Montagem completa de computadores sob medida conforme as necessidades do cliente.', 600.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Services" ("Id","Class","Title","Description","Price","Status") VALUES (26, 'Software', 'Formatação de Computadores (Windows/Linux)', 'Formatação completa do sistema operacional Windows ou Linux com reinstalação limpa.', 90.00, '') ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "HomePageInformation" ("Id","ServicesPerformed","SatisfiedCustomers","DeliveryTime","SatisfactionIndex") VALUES (1, 3, 3, 96, 100) ON CONFLICT ("Id") DO NOTHING;

SELECT setval(pg_get_serial_sequence('"Services"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Services"), 1));

COMMIT;
