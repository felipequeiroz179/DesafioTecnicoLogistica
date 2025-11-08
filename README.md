# Desafio Técnico – Sistema de Processamento de Pedidos

Este projeto implementa o back-end de um sistema de gerenciamento de entregas usando uma arquitetura orientada a eventos, com foco em consistência total de dados, resiliência e baixa latência.

## Arquitetura

A solução utiliza uma arquitetura de microsserviços desacoplada, composta por:

* **`OrderApi`**: Uma API .NET 8 responsável por receber e consultar pedidos.
* **`OrderProcessor`**: Um Worker Service .NET 8 que processa os pedidos de forma assíncrona.
* **`MySQL`**: Banco de dados relacional para persistência de pedidos, histórico e eventos.
* **`RabbitMQ`**: Message broker para a fila de eventos de pedidos.

### Diagrama de Fluxo

1.  **Criação (Transação Atômica):**
    `[Cliente] -> [OrderApi] -> (Transação) -> [DB: Orders] + [DB: OutboxEvents]`

2.  **Publicação (Relay):**
    `[OutboxRelay (na API)] -> (Lê Outbox) -> [RabbitMQ: order-events]`

3.  **Processamento (Assíncrono):**
    `[OrderProcessor] -> (Consome Fila) -> (Transação) -> [DB: Orders (Update)] + [DB: OrderHistory] + [DB: OutboxEvents (Próximo)]`


    <img width="1370" height="886" alt="image" src="https://github.com/user-attachments/assets/29462874-e647-400c-9db5-3c2e044794bc" />


## Estratégias de Consistência e Resiliência

* **Consistência Total (Transactional Outbox Pattern):** A API garante que um pedido e seu respectivo evento sejam salvos na mesma transação de banco de dados (`Orders` + `OutboxEvents`). Um "Relay" em background na API é responsável por ler a tabela `OutboxEvents` e publicar no RabbitMQ, garantindo "pelo menos uma entrega" (at-least-once delivery) e que nenhum evento seja perdido, mesmo se a fila estiver offline.

* **Resiliência (Idempotência):** O `OrderProcessor` é idempotente. Antes de processar qualquer evento, ele verifica o estado atual do pedido no banco. Se um evento duplicado for recebido (ex: `OrderReceived` para um pedido que já está `EmTransporte`), o evento é simplesmente ignorado (ACK), evitando duplicidade de processamento.

* **Logs Estruturados:** Ambos os serviços (`Api` e `Processor`) usam **Serilog** com formatação JSON para logs estruturados, permitindo o rastreamento fácil de um `OrderId` por todo o fluxo.

## Tecnologias Utilizadas

* **.NET 8** (SDK)
* **ASP.NET 8 Web API** (Para `OrderApi`)
* **.NET 8 Worker Service** (Para `OrderProcessor`)
* **MySQL 8.0** (Banco de dados)
* **RabbitMQ 3** (Message Broker)
* **Entity Framework Core 8** (ORM)
* **Pomelo.EntityFrameworkCore.MySql** (Driver MySQL)
* **Serilog** (Logs Estruturados)
* **Docker Compose** (Orquestração de Infraestrutura)

## Como Executar (Instruções para Avaliador)

Siga os passos abaixo para executar o projeto localmente.

**Pré-requisitos:**
1.  [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2.  [Docker Desktop](https://www.docker.com/products/docker-desktop/)

---

### 1. Iniciar a Infraestrutura (Banco e Fila)

No terminal, na raiz do projeto (`DesafioTecnicoLogistica/`), suba os contêineres:

Bash
docker-compose up -d


### 2. Executar a API (OrderApi)
Abra um novo terminal e execute os comandos abaixo. A API é responsável por aplicar as migrações no banco de dados.

## Entre na pasta da API
cd DeliverySystem.OrderApi

## (Opcional) Aplique as migrações manualmente (a API também faz isso)
dotnet ef database update

## Execute a API
dotnet run

A API estará rodando e disponível em http://localhost:5xxx e https://localhost:7xxx.

## 3. Executar o Processador (OrderProcessor)
Abra um terceiro terminal para executar o worker que consome a fila.

## Entre na pasta do Worker
cd DeliverySystem.OrderProcessor

## Execute o Worker
dotnet run

O worker irá se conectar ao RabbitMQ e aguardar por mensagens.
Você pode usar o Postman, Insomnia, Swagger ou cURL para testar.

Link do swagger: http://localhost:5222/swagger/index.html
