@spending
Feature: Spending

  @SPD-01 @F8.1 @F8.2 @F8.3
  Scenario: Spending totals group actual costs by provider and model
    Given the OpenRouter image model catalog:
      | id                                 | name  | seed | price per MP |
      | black-forest-labs/flux.2-klein-4b | Klein | true | 0.014        |
      | vendor/alpha                       | Alpha | true | 0.03         |
    And the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                  |
      | generate_images | A pair of garden lanterns. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response | width | height | cost  |
      | picture  | 64    | 64     | 0.014 |
    And the provider answers generation for model "vendor/alpha" with:
      | response | width | height | cost |
      | picture  | 64    | 64     | 0.03  |
    When the user creates a new session
    And the user selects image models for this run:
      | model id                         |
      | black-forest-labs/flux.2-klein-4b |
      | vendor/alpha                     |
    And the user sends the message "Draw two garden lanterns"
    Then model "black-forest-labs/flux.2-klein-4b" has a done generation card with an image
    And model "vendor/alpha" has a done generation card with an image
    When the spending clock advances 1 second
    When the user opens the spending screen for the current day
    Then spending total is "$0.044"
    And spending has these provider and model totals:
      | provider  | model                              | total | operations |
      | openrouter | black-forest-labs/flux.2-klein-4b | 0.014 | 1          |
      | openrouter | vendor/alpha                      | 0.03  | 1          |
    And spending has 1 unknown operations

  @SPD-02 @F8.1 @F8.3
  Scenario: Spending excludes generations without a reported cost
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                  |
      | generate_images | A blue whale underwater. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response     | width | height |
      | unknown cost | 64    | 64     |
    When the user creates a new session
    And the user sends the message "Draw a whale"
    Then model "black-forest-labs/flux.2-klein-4b" has a done generation card with an image
    When the spending clock advances 1 second
    And the user opens the spending screen for the current day
    Then spending total is "$0"
    And spending has 2 unknown operations

  @SPD-03 @F8.3
  Scenario: A Sunday expense is outside the next week's total but remains in the month
    Given the spending clock is at "2026-01-04T12:00:00+00:00" in time zone "UTC"
    And the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                  |
      | generate_images | A mountain lake at dawn. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response | width | height | cost |
      | picture  | 64    | 64     | 0.02 |
    When the user creates a new session
    And the user sends the message "Draw a mountain lake"
    Then model "black-forest-labs/flux.2-klein-4b" has a done generation card with an image
    When the current time advances by 1 day
    And the user opens the spending screen for the current week
    Then spending total is "$0"
    When the user selects the spending month
    Then spending total is "$0.02"

  @SPD-04 @F8.4 @ignore @bug-280
  Scenario: The expensive-run threshold warns before an assistant message is sent
    Given the user has saved an OpenRouter key
    And the next run estimate is below the configured spending threshold
    When the user sends a message that would start a run
    Then a spending warning is shown before the message is sent
    And the message can still be sent

  @SPD-05 @F8.4
  Scenario: The monthly limit persists after restarting the app
    Given the user has a saved OpenRouter key
    When the user opens the spending screen for the current month
    And the user saves a monthly spending limit of "42.50"
    When the user restarts the app
    And the user opens the spending screen for the current month
    Then the monthly spending limit is "42.50"
