@runs
Feature: Runs and generations

  @GEN-01 @S3.1 @S3.2
  Scenario: Two models receive the same seeds across two runs each
    Given the OpenRouter image model catalog:
      | id                                | name          | seed  | price per MP |
      | black-forest-labs/flux.2-klein-4b | Flux Klein    | true  | 0.014        |
      | vendor/alpha                      | Alpha         | true  | 0.02         |
      | vendor/beta                       | Beta No Seed  | false | unknown      |
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                                              |
      | generate_images | A mountain cabin beneath an aurora in a clear sky. |
    And the seed source returns:
      | seed |
      | 11   |
      | 22   |
    When the user creates a new session
    And the user selects these image models with two runs per model:
      | model id                         |
      | black-forest-labs/flux.2-klein-4b |
      | vendor/alpha                     |
    And the user sends the message "Draw an aurora over a cabin"
    Then the run shows four generation cards with seeds 11 and 22 for each model

  @GEN-02 @S3.3
  Scenario: A model without seed support omits seed from requests and cards
    Given the OpenRouter image model catalog:
      | id                                | name         | seed  | price per MP |
      | black-forest-labs/flux.2-klein-4b | Flux Klein   | true  | 0.014        |
      | vendor/alpha                      | Alpha        | true  | 0.02         |
      | vendor/beta                       | Beta No Seed | false | unknown      |
    And the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                                      |
      | generate_images | A glass greenhouse filled with orange poppies. |
    When the user creates a new session
    And the user selects these image models with two runs per model:
      | model id    |
      | vendor/beta |
    And the user sends the message "Draw orange poppies"
    Then model "vendor/beta" receives requests without a seed and the cards show seed not supported

  @GEN-03 @S3.4 @S3.5
  Scenario: One model failure does not change another model result
    Given the OpenRouter image model catalog:
      | id                                | name       | seed | price per MP |
      | black-forest-labs/flux.2-klein-4b | Flux Klein | true | 0.014        |
      | vendor/alpha                      | Alpha      | true | 0.02         |
    And the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                                         |
      | generate_images | A red fox sleeping beneath a birch tree. |
    And the provider answers generation for model "vendor/alpha" with:
      | response | status | body                          |
      | error    | 500    | {"error":{"message":"failure"}} |
    When the user creates a new session
    And the user selects these image models with two runs per model:
      | model id                         |
      | black-forest-labs/flux.2-klein-4b |
      | vendor/alpha                     |
    And the user sends the message "Draw a sleeping fox"
    Then model "vendor/alpha" generations are failed and model "black-forest-labs/flux.2-klein-4b" generations are done

  @GEN-04 @S3.5 @F6.5 @F9.2
  Scenario: Provider filtering has its own state and does not fail another model
    Given the OpenRouter image model catalog:
      | id                                | name       | seed | price per MP |
      | black-forest-labs/flux.2-klein-4b | Flux Klein | true | 0.014        |
      | vendor/alpha                      | Alpha      | true | 0.02         |
    And the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                                  |
      | generate_images | A red fox in a snowy forest. |
    And the provider answers generation for model "vendor/alpha" with:
      | response  |
      | filtering |
    When the user creates a new session
    And the user selects these image models with two runs per model:
      | model id                         |
      | black-forest-labs/flux.2-klein-4b |
      | vendor/alpha                     |
    And the user sends the message "Draw a fox in snow"
    Then model "vendor/alpha" generations are filtered and model "black-forest-labs/flux.2-klein-4b" generations are done

  @GEN-05 @S3.5
  Scenario: A running generation becomes done when the provider answers
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                                   |
      | generate_images | A blue whale beneath a moonlit sea. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response |
      | hold     |
    When the user creates a new session
    And the user sends the message "Draw a whale underwater"
    Then model "black-forest-labs/flux.2-klein-4b" has an active generation card
    When the provider completes generation for model "black-forest-labs/flux.2-klein-4b"
    Then model "black-forest-labs/flux.2-klein-4b" has a done generation card with an image

  @GEN-06 @S3.6 @F7.2
  Scenario: A generated image file remains attached to its card after restart
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                      |
      | generate_images | A blue whale in clear water. |
    When the user creates a new session
    And the user sends the message "Draw a whale"
    Then the generated image is saved before the card shows it
    When the user restarts the app
    Then the user opens the generated image session
    Then the generated image card still shows the same saved file

  @GEN-07 @S3.7
  Scenario: The generation card shows provider cost independently of the current culture
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                     |
      | generate_images | A blue whale at sunset. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response | width | height | cost  |
      | picture  | 64    | 64     | 0.125 |
    And the current culture is "fr-FR"
    When the user creates a new session
    And the user sends the message "Draw a whale"
    Then the generation card shows model "black-forest-labs/flux.2-klein-4b", seed, and cost "$0.125"

  @GEN-08 @S3.9
  Scenario: The run summary updates as generations finish
    Given the OpenRouter image model catalog:
      | id                                | name       | seed | price per MP |
      | black-forest-labs/flux.2-klein-4b | Flux Klein | true | 0.014        |
      | vendor/alpha                      | Alpha      | true | 0.02         |
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                          |
      | generate_images | Two scenes with mountain lakes. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response |
      | hold     |
    When the user creates a new session
    And the user selects image models for this run:
      | model id                         |
      | black-forest-labs/flux.2-klein-4b |
      | vendor/alpha                     |
    And the user sends the message "Draw two lakes"
    Then the run summary shows "1 of 2 done"
    When the provider completes generation for model "black-forest-labs/flux.2-klein-4b"
    Then the run summary shows "2 of 2 done"

  @GEN-09 @S3.10
  Scenario: The image card shows pixel dimensions and aspect ratio
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                         |
      | generate_images | A wide valley at golden hour. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response | width | height | cost |
      | picture  | 120   | 80     | 0.01 |
    When the user creates a new session
    And the user sends the message "Draw a wide valley"
    Then the generation card shows image dimensions "120×80 px · 3:2"

  @GEN-10 @S3.1 @S5.2
  Scenario: Changing selected models during a run affects only the next run
    Given the OpenRouter image model catalog:
      | id                                | name       | seed | price per MP |
      | black-forest-labs/flux.2-klein-4b | Flux Klein | true | 0.014        |
      | vendor/alpha                      | Alpha      | true | 0.02         |
      | vendor/beta                       | Beta       | true | 0.03         |
    And the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                 |
      | generate_images | The first image prompt. |
    And the provider answers generation for model "black-forest-labs/flux.2-klein-4b" with:
      | response |
      | hold     |
    When the user creates a new session
    And the user selects image models for this run:
      | model id                         |
      | black-forest-labs/flux.2-klein-4b |
      | vendor/alpha                     |
    And the user sends the message "Draw the first image"
    Then the current run has two cards for models "black-forest-labs/flux.2-klein-4b" and "vendor/alpha"
    When the user selects image models for this run:
      | model id    |
      | vendor/beta |
    Then the current run has two cards for models "black-forest-labs/flux.2-klein-4b" and "vendor/alpha"
    When the provider completes generation for model "black-forest-labs/flux.2-klein-4b"
    And the next assistant response is:
      | response        | prompt                  |
      | generate_images | The second image prompt. |
    And the user sends the message "Draw the second image"
    Then the next run has one done card for model "vendor/beta"

  @GEN-11 @F4.4
  Scenario: The next run estimate sums known selected model prices
    Given the OpenRouter image model catalog:
      | id                                | name       | seed | price per MP | price per image |
      | black-forest-labs/flux.2-klein-4b | Flux Klein | true | unknown      | 0.02            |
      | vendor/alpha                      | Alpha      | true | unknown      | 0.03            |
    And the user has a saved OpenRouter key
    When the user creates a new session
    And the user selects image models for this run:
      | model id                         |
      | black-forest-labs/flux.2-klein-4b |
      | vendor/alpha                     |
    Then the next run estimate is "$0.05"
