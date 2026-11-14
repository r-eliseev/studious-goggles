@search
Feature: Session search

  @SRCH-01 @S1.4
  Scenario: Word search finds sessions by title and generation prompt
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response        | prompt                                    |
      | generate_images | A luminous glasshouse filled with orchids. |
    When the user creates a new session
    And the user sends the message "Draw a silver heron"
    Then model "black-forest-labs/flux.2-klein-4b" has a done generation card with an image
    When the user returns to the session list
    And the user searches sessions for "silver heron"
    Then the search results include the session titled "Draw a silver heron"
    When the user searches sessions for "luminous glasshouse"
    Then the search results include the session titled "Draw a silver heron"

  @SRCH-02 @S1.4
  Scenario: Word search shows no results and clearing restores the session list
    Given the user has a saved OpenRouter key
    When the user creates a new session
    And the user returns to the session list
    And the user searches sessions for "no matching words"
    Then the search shows no results
    When the user clears the session search
    Then session search is cleared and the session list is restored

  @SRCH-03 @S1.4 @ADR-0016
  Scenario: Semantic search is off by default and enabling it indexes session history
    Given the user has a saved OpenRouter key
    And the assistant answers with:
      | response | text                         |
      | text     | A blue sailboat on the lake. |
    When the user creates a new session
    And the user sends the message "Describe a sailboat"
    When the user returns to the session list
    Then semantic search is off
    When the user enables semantic search in Settings
    Then the semantic index is ready
    And the fake received embedding requests

  @SRCH-04 @S1.4 @ADR-0016
  Scenario: Semantic and word search show separate session results
    Given the fake embeddings return these vectors:
      | text                  | vector |
      | Red cabin             | 1,0,0  |
      | Golden dunes          | 0,1,0  |
      | ancient monuments     | 0,1,0  |
    And the user has a saved OpenRouter key
    And the assistant answers with:
      | response | text                          |
      | text     | A red cabin in snowy woods.   |
    When the user creates a new session
    And the user sends the message "Red cabin"
    When the user returns to the session list
    Given the assistant answers with:
      | response | text                           |
      | text     | Golden dunes under a pale sky. |
    When the user creates a new session
    And the user sends the message "Golden dunes"
    When the user returns to the session list
    And the user enables semantic search in Settings
    When the user returns to the session list
    And the user selects semantic search
    And the user searches sessions for "ancient monuments"
    Then semantic search ranks the session titled "Golden dunes" first
    When the user selects word search
    And the user searches sessions for "ancient monuments"
    Then the search shows no results
    When the user searches sessions for "Red cabin"
    Then the search results include the session titled "Red cabin"
    And the search results exclude the session titled "Golden dunes"

  @SRCH-05 @S1.4 @ADR-0016
  Scenario: Settings show index status and changing the embedding model reindexes history
    Given the fake embedding model catalog:
      | id                  | name          |
      | baai/bge-m3          | BGE M3        |
      | vendor/e5-small      | E5 Small      |
    And the user has a saved OpenRouter key
    And the assistant answers with:
      | response | text                       |
      | text     | A quiet lake at sunrise.   |
    When the user creates a new session
    And the user sends the message "Describe a quiet lake"
    When the user returns to the session list
    And the user opens settings from the session list
    Then the semantic index is off
    When the user enables semantic search in Settings
    Then the semantic index is ready
    When the user changes the embedding model to "vendor/e5-small"
    Then embeddings were requested with model "vendor/e5-small"
    And the semantic index is ready

  @SRCH-06 @S1.4 @ADR-0016
  Scenario: Settings report an embedding model that is no longer available
    Given the fake embedding model catalog:
      | id         | name   |
      | baai/bge-m3 | BGE M3 |
    And semantic search is enabled with unavailable model "vendor/missing"
    And the user has a saved OpenRouter key
    When the user opens settings from the session list
    Then Settings report the embedding model is unavailable
