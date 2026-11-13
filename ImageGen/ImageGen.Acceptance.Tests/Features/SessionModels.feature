@models
Feature: Session models and generation parameters

  @MOD-01 @S5.1 @S5.2
  Scenario: Two selected models run three times each
    Given the OpenRouter image model catalog:
      | id                                | name       | seed | price per MP |
      | black-forest-labs/flux.2-klein-4b | Flux Klein | true | 0.014        |
      | vendor/alpha                      | Alpha      | true | 0.02         |
    And the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                   |
      | generate_images | Two models, three runs. |
    When the user creates a new session
    And the user selects models "black-forest-labs/flux.2-klein-4b" and "vendor/alpha" with 3 runs per model
    And the user sends the message "Generate six images"
    Then the run shows six done cards for the two selected models

  @MOD-02 @S5.1
  Scenario: The runs control declares its allowed range
    Given the user has a saved OpenRouter key
    When the user creates a new session
    And the user opens session models
    Then the runs control is limited to 1 through 10

  @MOD-03 @S5.1
  Scenario: Search filters image models by name and id
    Given the OpenRouter image model catalog:
      | id          | name          | seed  | price per MP |
      | vendor/alpha | Alpha Render  | true  | 0.02         |
      | vendor/beta  | Beta Render   | false | unknown      |
    And the user has a saved OpenRouter key
    When the user creates a new session
    And the user opens session models
    Then searching models for "Beta Render" shows only "vendor/beta"
    And searching models for "vendor/alpha" shows only "vendor/alpha"

  @MOD-04 @S5.5
  Scenario: Image models are grouped by vendor prefix
    Given the OpenRouter image model catalog:
      | id              | name          | seed  | price per MP |
      | black-forest-labs/flux | Flux      | true  | 0.014        |
      | ~google/imagen  | Imagen        | true  | 0.03         |
      | openai/dall-e   | DALL-E        | false | unknown      |
    And the user has a saved OpenRouter key
    When the user creates a new session
    And the user opens session models
    Then image model groups include "black-forest-labs", "google", and "openai"

  @MOD-05 @S5.6
  Scenario: Prices use output image billing and unknown prices stay unknown
    Given the OpenRouter image model catalog:
      | id           | name  | seed  | price per MP |
      | black-forest-labs/flux.2-klein-4b | Flux Klein | true  | 0.014        |
      | vendor/alpha | Alpha | true  | 0.02         |
      | vendor/beta  | Beta  | false | unknown      |
    And the user has a saved OpenRouter key
    When the user creates a new session
    And the user opens session models
    Then model "vendor/alpha" shows price "$0.02 / MP"
    And model "vendor/beta" shows price "price unknown"

  @MOD-06 @S5.6
  Scenario: The model catalog is cached until refreshed
    Given the OpenRouter image model catalog:
      | id           | name  | seed  | price per MP |
      | vendor/alpha | Alpha | true  | 0.02         |
    And the user has a saved OpenRouter key
    When the user creates a new session
    And the user opens session models
    And the user snapshots model catalog requests
    And the user cancels model changes
    And the user reopens session models
    Then reopening models does not request the catalog again
    When the OpenRouter image model catalog changes to:
      | id           | name  | seed  | price per MP |
      | vendor/alpha | Alpha | true  | 0.04         |
    And the user refreshes the model catalog
    Then model "vendor/alpha" shows price "$0.04 / MP"

  @MOD-07 @S5.1
  Scenario: Canceling model changes keeps the saved selection and run count
    Given the OpenRouter image model catalog:
      | id           | name  | seed  | price per MP |
      | black-forest-labs/flux.2-klein-4b | Flux Klein | true  | 0.014        |
      | vendor/alpha | Alpha | true  | 0.02         |
    And the user has a saved OpenRouter key
    When the user creates a new session
    And the user opens session models
    And the user selects "vendor/alpha" and 3 runs per model
    And the user cancels model changes
    And the user reopens session models
    Then only the default image model is selected with 1 run per model

  @MOD-08 @F4.2
  Scenario: Saved session parameters are sent in the next image request
    Given the OpenRouter image model catalog:
      | id                                | name       | seed | price per MP | aspect ratios | resolutions | formats     | qualities |
      | black-forest-labs/flux.2-klein-4b | Flux Klein | true | 0.014        | 1:1,16:9     | 1K,2K       | png,jpeg,webp | high,low  |
    And the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                 |
      | generate_images | A panoramic coastline. |
    When the user creates a new session
    And the user saves session parameters aspect ratio "16:9", resolution "2K", and format "webp"
    And the user sends the message "Draw a panoramic coastline"
    Then the image request contains aspect ratio "16:9", resolution "2K", and format "webp"
