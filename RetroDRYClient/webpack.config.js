const path = require('path');
module.exports = {
    entry: './index.js',
    devtool: 'inline-source-map',
    mode: 'development',
    module: {
        rules: [
            {
                test: /\.tsx?$/,
                use: 'ts-loader',
                exclude: /node_modules/
            }
        ]
    },
    resolve: {
        extensions: [ '.tsx', '.ts', '.js']
    },
    output: {
        filename: 'retrodry.js',
        path: path.resolve(__dirname, 'dist'),
        library: 'retrodry',
        libraryTarget: 'umd'
    }
}