# VisArch-Database-Manager
A comprehensive database management system for Unity applications, supporting both local and cloud-based databases with asynchronous operations, generic programming, and robust CRUD functionalities.

# Overview
VisArch-Database-Manager is a comprehensive database management system designed specifically for Unity applications. This system supports both local and cloud-based databases and provides robust asynchronous operations, generic programming techniques, and a full suite of CRUD (Create, Read, Update, Delete) functionalities to manage complex data interactions seamlessly within Unity environments.

# Features
- **Local and Cloud Database Support**: Easily switch between local and cloud databases using a simple toggle.
- **Asynchronous Operations**: All database interactions are fully asynchronous, ensuring smooth performance without blocking the main Unity thread.
- **Generic Programming**: Generic methods allow for flexible data management across various types.
- **CRUD Functionalities**: Includes methods for creating, reading, updating, and deleting data across different database types.
- **Seamless Integration**: Optimized for Unity, leveraging the Unity Editor and .NET frameworks.

# Getting Started
**Prerequisites**
- Unity 2020.3 or later
- .NET Framework 4.x
- Mono.Data.Sqlite (for local SQLite database support)

#Installation
- Clone the repository to your local machine.
- Open your Unity project and add the VisArch-Database-Manager folder to your Assets directory.

#Usage
**Initialize the Database Manager:**
1. Create an instance of GlobalDatabaseManager in your Unity scene.
2.Configure the DatabaseMode (either local or cloud) to switch between local and cloud databases.

**Perform CRUD Operations:**
-Use the provided methods like InsertAsync, SelectAllAsync, UpdateSpecificRowAsync, and DeleteAsync to manage your database records.
