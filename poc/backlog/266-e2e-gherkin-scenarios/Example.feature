Feature: Reqnroll with Allure

  @e2e
  Scenario: A tagged Gherkin scenario is reported
    Given the POC is ready
    Then the scenario passes

  @e2e-only
  Scenario: An E2E-only scenario is generated
    Given the POC is ready
