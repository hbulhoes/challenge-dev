# Desafio de c�digo
Esta � a implementa��o do projeto de testes proposto pela Wappa. Dentro do escopo descrito na [apresenta��o do desafio](https://github.com/wappamobile/challenge-dev), procurei implementar uma aplica��o *serverless* utilizando os seguintes recursos e componentes:

 * **ASP.Net Core WebAPI** como plataforma de desenvolvimento dos servi�os solicitados;
 * **AWS Lambda** como ambiente de execu��o da aplica��o *serverless* implementada em WebAPI;
 * **AWS API Gateway** como porta de entrada REST dos servi�os executados pelo Lambda;
 * **AWS CloudFormation** para o *deploy* por via declarativa dos recursos de nuvem requeridos pela aplica��o;
 * **DynamoDB** como reposit�rio NoSQL de informa��es;
 * **xUnit** para a composi��o dos testes de integra��o utilizados em todo o ciclo de desenvolvimento;
 * **RestSharp** para a chamada de funcionalidades do Google Maps API.

## Organiza��o do projeto

A solu��o � dividida em dois projetos de C#: **DriverCatalogService** um cont�m a WebAPI requerida, e **DriverCatalogService.Tests** cont�m os testes de integra��o.

### DriverCatalogService
A estrutura deste projeto cont�m:
* _Controllers_: inclui os controllers que implementam as funcionalidades de cat�logo de motoristas (DriverCatalogController) e listagem do cat�logo (DriverCatalogQueryController).
* _Infrastructure_: inclui as defini��es/interfaces dos servi�os consumidos pela aplica��o, como o reposit�rio de dados (IRepository/DynamoDBRepository) e a API de geolocaliza��o (IGeoLocator/GoogleGeoLocator).
* _Models_: inclui as defini��es de estruturas de dados de Motorista (Driver), Endere�o (Address), Carro (Car), entre outras.
* _Classes de bootstrapping_: c�digo de inicializa��o, incluindo defini��es de IOC e carga de configura��o s�o localizados nas classes Startup, LocalEntryPoint e LambdaEntryPoint.
* _Scripts do CloudFormation_: para automatiza��o do provisionamento de recursos da AWS, todos os requisitos de nuvem da aplica��o s�o declarados no arquivo serverless.template.


## Requisitos para desenvolvimento

� necess�rio instalar as ferramentas [Amazon.Lambda.Tools](https://github.com/aws/aws-extensions-for-dotnet-cli#aws-lambda-amazonlambdatools) e [AWS Toolkit for Visual Studio](https://aws.amazon.com/pt/visualstudio/). O primeiro extende as capacidades da CLI do .Net Core, e o segundo adiciona funcionalidades de gerenciamento do *stack* AWS ao VisualStudio.

Para instalar o AWS Lambda Tools, basta utilizar o seguinte comando Powershell:
```
    dotnet tool install -g Amazon.Lambda.Tools
```

Uma vez instaladas as ferramentas, j� � poss�vel executar os testes integrados. Uma forma pr�tica � iniciar os testes a partir de linha de comando:

```
    cd "DriverCatalogService/test/DriverCatalogService.Tests"
    dotnet test
```


## Publica��o no ambiente AWS

Basta acionar o bot�o *Publish to AWS* no menu de contexto do Solution Explorer, diretamente no projeto **DriverCatalogService**. Ser� aberta uma janela "Stacj View" com o andamento da publica��o.

Ap�s o *deploy* bem-sucedido, o API Gateway ter� publicado um *endpoint* de produ��o a partir do qual podem ser acionadas as chamadas de API.


## Perguntas que talvez sejam feitas
No in�cio do desenvolvimento, e tamb�m durante o projeto, algumas decis�es importantes foram tomadas. Aqui est� a raz�o por tr�s de algumas delas:

#### Por que AWS?
� o provedor de servi�os com o qual eu tenho maior familiaridade. Tenho experi�ncias recentes bem sucedidas com Lambda e CloudFormation, e quis re-aplicar esse conhecimento.

#### Por que serverless?
O desafio pareceu ser uma boa prova de conceito para a constru��o e deploy de servi�os serverless.

#### Por que nomear identificadores em ingl�s?
Na minha opini�o, a l�ngua inglesa facilita a tarefa de "dar nomes" a partir de verbos e prefixos consolidados, como "Get", "Find", "enumeration", "index" etc. Escrevendo em portugu�s podem ser gerados nomes esdr�xulos como "GetMotorista", "FindEndereco", "carroCollection" etc, que me soam estranhos.