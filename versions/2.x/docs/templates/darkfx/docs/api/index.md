# Petstore API

This is a sample server Petstore server.  You can find out more about Swagger at [http://swagger.io](http://swagger.io) or on [irc.freenode.net, #swagger](http://swagger.io/irc/).  For this sample, you can use the api key `special-key` to test the authorization filters.

## About

| Url                                                               | Version | Contact                                                         | Terms of Service                                                        | License                                                                 |
| ----------------------------------------------------------------- | ------- | --------------------------------------------------------------- | ----------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| [petstore.swagger.io/v2](http://petstore.swagger.io/v2 "API url") | 1.0.0   | [apiteam@swagger.io](mailto:apiteam@swagger.io "Contact Email") | [http://swagger.io/terms/](http://swagger.io/terms/ "Terms of Service") | [Apache 2.0](http://www.apache.org/licenses/LICENSE-2.0.html "License") |

## Schemes

| Scheme |
| ------ |
| https  |
| http   |

## Endpoints

### pet

#### POST

##### addPet

Add a new pet to the store

##### Expected Response Types

| Response | Reason        |
| -------- | ------------- |
| 405      | Invalid input |

##### Parameters

| Name | In   | Description                                    | Required? | Type                   |
| ---- | ---- | ---------------------------------------------- | --------- | ---------------------- |
| body | body | Pet object that needs to be added to the store | true      | [Pet](#pet-definition) |

##### Content Types Produced

| Produces         |
| ---------------- |
| application/xml  |
| application/json |

##### Content Types Consumed

| Consumes         |
| ---------------- |
| application/json |
| application/xml  |

##### Security

| Id            | Scopes     |
| ------------- | ---------- |
| petstore_auth | write:pets |

#### PUT

##### updatePet

Update an existing pet

##### Expected Response Types

| Response | Reason               |
| -------- | -------------------- |
| 400      | Invalid ID supplied  |
| 404      | Pet not found        |
| 405      | Validation exception |

##### Parameters

| Name | In   | Description                                    | Required? | Type                   |
| ---- | ---- | ---------------------------------------------- | --------- | ---------------------- |
| body | body | Pet object that needs to be added to the store | true      | [Pet](#pet-definition) |

##### Content Types Produced

| Produces         |
| ---------------- |
| application/xml  |
| application/json |

##### Content Types Consumed

| Consumes         |
| ---------------- |
| application/json |
| application/xml  |

##### Security

| Id            | Scopes     |
| ------------- | ---------- |
|               |            |
| petstore_auth | write:pets |

### pet/findByStatus

#### GET

##### findPetsByStatus

Finds Pets by status
Multiple status values can be provided with comma separated strings

##### Expected Response Types

| Response | Reason               |
| -------- | -------------------- |
| 200      | successful operation |
| 400      | Invalid status value |

##### Parameters

| Name   | In    | Description                                         | Required? | Type  |
| ------ | ----- | --------------------------------------------------- | --------- | ----- |
| status | query | Status values that need to be considered for filter | true      | array |

##### Content Types Produced

| Produces         |
| ---------------- |
| application/xml  |
| application/json |

##### Content Types Consumed

| Consumes |
| -------- |
| None     |

##### Security

| Id            | Scopes     |
| ------------- | ---------- |
| petstore_auth | write:pets |

## Security Definitions

| Id            | Type   | Flow     | Authorization Url                           | Name    | In     | Scopes                  |
| ------------- | ------ | -------- | ------------------------------------------- | ------- | ------ | ----------------------- |
| petstore_auth | oauth2 | implicit | https://petstore.swagger.io/oauth/authorize |         |        | :write:pets, :read:pets |
| api_key       | apiKey |          |                                             | api_key | header |                         |

| Scope      | Description                 |
| ---------- | --------------------------- |
|            | modify pets in your account |
| write:pets | read your pets              |

## Definitions

### ApiResponse Definition

| Property | Type    | Format |
| -------- | ------- | ------ |
| code     | integer | int32  |
| type     | string  |        |
| message  | string  |        |

### Category Definition

| Property | Type    | Format |
| -------- | ------- | ------ |
| id       | integer | int64  |
| name     | string  |        |

| Property | Type    | Format    |
| -------- | ------- | --------- |
| id       | integer | int64     |
| petId    | integer | int64     |
| quantity | integer | int32     |
| shipDate | string  | date-time |
| status   | string  |           |
| complete | boolean |           |

### Pet Definition

| Property  | Type                             | Format |
| --------- | -------------------------------- | ------ |
| id        |                                  | int64  |
| category  | integer                          |        |
| name      | [Category](#category-definition) |        |
| photoUrls | string                           |        |
| tags      | array                            |        |
| status    | array                            |        |

### Tag Definition

| Property | Type    | Format |
| -------- | ------- | ------ |
| id       | integer | int64  |
| name     | string  |        |

### User Definition

| Property   | Type    | Format |
| ---------- | ------- | ------ |
| id         | integer | int64  |
| username   | string  |        |
| firstName  | string  |        |
| lastName   | string  |        |
| email      | string  |        |
| password   | string  |        |
| phone      | string  |        |
| userStatus | integer | int32  |

## Additional Resources

[Find out more about Swagger](http://swagger.io "External Documentation")
