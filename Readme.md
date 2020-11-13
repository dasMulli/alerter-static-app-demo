# Alerter Sample

This is a sample for an Azure Static Web App with a backing Azure Functions App allowing to send out alerts to employees.

The sample uses Azure Active Directory for authentication and makes use of the Microsoft Graph APIs to query user and group information.

The sample application allows to send templated alerts to a group of people. The client (Frontend) authenticates the user and queries the Graph for Groups to send to.
The backend API then receives requests for Alerts and may use service connections not exposed in the client to send alerts (this can be implemented with e.g. SMS APIs).

Since this is a basic sample for serverless M365 integrated applications, further extensions coud be:
* Packaging as an MS Teams Application
* Alerting people managed in a SharePoint list
* Additional contact methods, e.g. e-mail